using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly SteamCmdService      _steamCmd = new();
    private readonly ServerProcessService _process  = new();
    private readonly DiscordService       _discord  = new();
    private readonly DispatcherTimer      _statusTimer;

    // Track previous status per server to detect transitions
    private readonly Dictionary<string, string> _prevStatus = new();
    // Set during intentional restart so we send Restart instead of Start
    private readonly HashSet<string> _restartingServers = new();

    public ObservableCollection<Server> Servers { get; } = new();

    [ObservableProperty] private Server? _selectedServer;

    // Install card state
    [ObservableProperty] private string _installBuildLabel  = "Not installed";
    [ObservableProperty] private string _installStatusLabel = "";
    [ObservableProperty] private bool   _installIsUpToDate;

    // Action feedback
    [ObservableProperty] private string _actionError = "";

    // Inline detail navigation
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveDetailTitle))]
    private object? _activeDetail;

    public string ActiveDetailTitle => ActiveDetail switch
    {
        OverviewViewModel       => "Overview",
        PlayersViewModel        => "Players",
        BackupsViewModel        => "Backups",
        ScheduleViewModel       => "Schedule",
        DiscordViewModel        => "Discord",
        ConsoleViewModel        => "Console",
        ConfigViewModel         => "Config",
        ServerSettingsViewModel => "Server Settings",
        _                      => ""
    };

    public MainViewModel()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += async (_, _) => await PollStatusAsync();
        _statusTimer.Start();
        LoadServers();
    }

    // ── Selection ────────────────────────────────────────────────────────────

    private Server? _previousSelected;
    partial void OnSelectedServerChanged(Server? value)
    {
        // Navigate home when switching servers
        if (value?.Id != _previousSelected?.Id)
        {
            _ = GoHomeAsync();
            ActionError = "";
        }
        RefreshInstallInfo(value);
        _previousSelected = value;
    }

    // ── Server list ──────────────────────────────────────────────────────────

    public void LoadServers()
    {
        Servers.Clear();
        foreach (var s in App.DataStore.GetAllServers())
            Servers.Add(s);
        SelectedServer ??= Servers.FirstOrDefault();
    }

    public void AddServer(Server server)
    {
        if (!string.IsNullOrWhiteSpace(server.InstallDir))
            System.IO.Directory.CreateDirectory(server.InstallDir);
        App.DataStore.SaveServer(server);
        Servers.Add(server);
        SelectedServer = server;
    }

    public void RemoveServer(Server server)
    {
        App.DataStore.DeleteServer(server.Id);
        Servers.Remove(server);
        SelectedServer = Servers.FirstOrDefault();
    }

    // ── Inline navigation ────────────────────────────────────────────────────

    [RelayCommand]
    private async Task GoHomeAsync()
    {
        if (ActiveDetail is IAsyncDisposable ad)
            await ad.DisposeAsync();
        ActiveDetail = null;
    }

    [RelayCommand]
    private async Task OpenOverviewAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        var vm = new OverviewViewModel(server);
        ActiveDetail = vm;
        _ = vm.StartAsync();
    }

    [RelayCommand]
    private async Task OpenBackupsAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        ActiveDetail = new BackupsViewModel(server);
    }

    [RelayCommand]
    private async Task OpenPlayersAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        var vm = new PlayersViewModel(server);
        ActiveDetail = vm;
        _ = vm.StartAsync();
    }

    [RelayCommand]
    private async Task OpenConsoleAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        var vm = new ConsoleViewModel(server);
        ActiveDetail = vm;
        _ = vm.StartAsync();
    }

    [RelayCommand]
    private async Task OpenConfigAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        ActiveDetail = new ConfigViewModel(server);
    }

    [RelayCommand]
    private async Task OpenScheduleAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        var vm = new ScheduleViewModel(server,
            stopServer:  () => _process.StopAsync(server, new Progress<string>()),
            startServer: () => Task.Run(() => _process.Start(server, out _)));
        ActiveDetail = vm;
    }

    [RelayCommand]
    private async Task OpenDiscordAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        ActiveDetail = new DiscordViewModel(server);
    }

    [RelayCommand]
    private async Task OpenServerSettingsAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        ActiveDetail = new ServerSettingsViewModel(server, OnDeleteServer);
    }

    private void OnDeleteServer(Server server)
    {
        RemoveServer(server);
        _ = GoHomeAsync();
    }

    [RelayCommand]
    private Task OpenInstallAsync()
    {
        // Install still uses a dialog (progress stream) — opened from code-behind for now.
        return Task.CompletedTask;
    }

    // ── Process commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void StartServer()
    {
        if (SelectedServer is not { } server) return;
        ActionError = "";
        try
        {
            if (!_process.Start(server, out var err))
                ActionError = err;
            else
                RefreshServerInList(server);
        }
        catch (Exception ex) { ActionError = ex.Message; }
    }

    [RelayCommand]
    private async Task StopServerAsync()
    {
        if (SelectedServer is not { } server) return;
        ActionError = "";
        try
        {
            RefreshServerInList(server);
            var progress = new Progress<string>(msg => ActionError = msg);
            await _process.StopAsync(server, progress);
            RefreshServerInList(server);
            ActionError = "";
        }
        catch (Exception ex) { ActionError = ex.Message; }
    }

    [RelayCommand]
    private async Task RestartServerAsync()
    {
        if (SelectedServer is not { } server) return;
        ActionError = "";
        try
        {
            _restartingServers.Add(server.Id);
            var progress = new Progress<string>(msg => ActionError = msg);
            await _process.StopAsync(server, progress);
            RefreshServerInList(server);

            if (!_process.Start(server, out var err))
            {
                ActionError = err;
                _restartingServers.Remove(server.Id);
            }
            else
            {
                ActionError = "";
                RefreshServerInList(server);
            }
        }
        catch (Exception ex) { ActionError = ex.Message; _restartingServers.Remove(server.Id); }
    }

    // ── Install info ─────────────────────────────────────────────────────────

    public void RefreshInstallInfo(Server? server)
    {
        if (server is null || string.IsNullOrWhiteSpace(server.InstallDir))
        {
            InstallBuildLabel  = "Not installed";
            InstallStatusLabel = "";
            InstallIsUpToDate  = false;
            return;
        }

        var isInstalled = _process.IsInstalled(server.InstallDir);
        var info        = isInstalled ? _steamCmd.GetInstallInfo(server.InstallDir) : null;

        if (!isInstalled || info is null)
        {
            InstallBuildLabel  = "Not installed";
            InstallStatusLabel = "Click to install via SteamCMD";
            InstallIsUpToDate  = false;
        }
        else
        {
            InstallBuildLabel  = $"Build {info.BuildId}";
            InstallStatusLabel = info.IsUpToDate ? "Up to date" : "Update available";
            InstallIsUpToDate  = info.IsUpToDate;
        }
    }

    // ── Status polling ───────────────────────────────────────────────────────

    private async Task PollStatusAsync()
    {
        var snapshot = Servers.ToList();
        foreach (var server in snapshot)
        {
            var (status, logTail) = await _process.ReconcileStatusAsync(server);
            if (status == server.Status) continue;

            var prev       = _prevStatus.GetValueOrDefault(server.Id, server.Status);
            var wasSelected = server == SelectedServer;
            server.Status  = status;
            _prevStatus[server.Id] = status;

            string? pendingError = null;
            if (status == ServerStatus.Stopped)
            {
                server.LastPid = null;
                App.DataStore.SaveServer(server);

                if (wasSelected)
                    pendingError = logTail is not null
                        ? $"Server stopped — last output:\n{logTail}"
                        : $"Server stopped (exit code {server.ExitCode}).";

                // Discord: crash vs intentional stop
                if (prev == ServerStatus.Running && !_restartingServers.Contains(server.Id))
                    _ = SendDiscordEventAsync(server, server.Discord.EventServerCrash, "crash");
                else if (prev == ServerStatus.Stopping)
                    _ = SendDiscordEventAsync(server, server.Discord.EventServerStop, "stop");
            }
            else if (status == ServerStatus.Running)
            {
                if (_restartingServers.Remove(server.Id))
                    _ = SendDiscordEventAsync(server, server.Discord.EventServerRestart, "restart");
                else
                    _ = SendDiscordEventAsync(server, server.Discord.EventServerStart, "start");
            }

            var idx = Servers.IndexOf(server);
            if (idx >= 0)
            {
                Servers.RemoveAt(idx);
                Servers.Insert(idx, server);
                if (wasSelected) SelectedServer = server;
            }

            if (pendingError is not null)
                ActionError = pendingError;
        }
    }

    // Called from MainWindow after install/update completes
    public async Task OnServerUpdatedAsync(Server server)
    {
        var disc = server.Discord ??= new Models.DiscordConfig();
        await SendDiscordEventAsync(server, disc.EventServerUpdated, "updated");
    }

    private async Task SendDiscordEventAsync(Server server, Models.DiscordEventConfig evt, string key)
    {
        var disc = server.Discord;
        if (disc is null || !disc.Enabled) return;
        await _discord.SendEventAsync(evt, disc.DefaultWebhook,
            $"{server.Id}:{key}", server.Name);
    }

    private void RefreshServerInList(Server server)
    {
        var idx = Servers.IndexOf(server);
        if (idx < 0) return;
        var wasSelected = server == SelectedServer;
        Servers.RemoveAt(idx);
        Servers.Insert(idx, server);
        if (wasSelected)
            SelectedServer = server;
    }
}
