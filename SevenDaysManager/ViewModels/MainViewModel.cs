using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
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
    [ObservableProperty] private bool   _installCheckBusy;

    // True while the selected server is not fully stopped — updating is blocked then
    [ObservableProperty] private bool   _selectedServerRunning;

    // Action feedback
    [ObservableProperty] private string _actionError = "";

    // Live stats (left panel mini dashboard)
    [ObservableProperty] private int    _livePlayers;
    [ObservableProperty] private float  _liveFps;
    [ObservableProperty] private int    _liveEntities;
    [ObservableProperty] private string _liveGameTime  = "—";
    [ObservableProperty] private string _liveUptime    = "—";
    [ObservableProperty] private string _liveServerPid = "—";
    [ObservableProperty] private double _liveCpuPct;
    [ObservableProperty] private double _liveRamPct;
    [ObservableProperty] private string _liveRamGbStr  = "—";

    private MetricsPoller?    _miniPoller;
    private Process?          _serverProcess;
    private readonly PerformanceCounter _cpuCounter =
        new("Processor", "% Processor Time", "_Total");

    // Inline detail navigation
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ActiveDetailTitle))]
    private object? _activeDetail;

    /// <summary>
    /// The version shown in the sidebar. Bound rather than hardcoded in the XAML, so it can't
    /// drift from UpdateService.CurrentVersion — which is what the update check compares
    /// against the GitHub release tag. One place to bump.
    /// </summary>
    public string AppVersion => $"V{Services.UpdateService.CurrentVersion}";

    public string ActiveDetailTitle => ActiveDetail switch
    {
        OverviewViewModel       => "Overview",
        PlayersViewModel        => "Players",
        BackupsViewModel        => "Backups",
        ScheduleViewModel       => "Schedule",
        DiscordViewModel        => "Discord",
        ModsViewModel           => "Mod Management",
        ConsoleViewModel        => "Console",
        ConfigViewModel         => "Config",
        ServerSettingsViewModel => "Server Settings",
        GameSettingsViewModel   => "Game Settings",
        _                      => ""
    };

    public MainViewModel()
    {
        _statusTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        _statusTimer.Tick += async (_, _) => await PollStatusAsync();
        _statusTimer.Start();
        LoadServers();
        _ = RefreshInstallInfoAsync(SelectedServer);
        _ = AutoStartServersAsync();
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
        _ = RefreshInstallInfoAsync(value);
        _previousSelected = value;
        UpdateSelectedServerRunning();

        if (value is { Status: ServerStatus.Running })
            StartLiveStats(value);
        else
            StopLiveStats();
    }

    // Updating via SteamCMD must not run while the server process is up (file locks / corruption).
    private void UpdateSelectedServerRunning() =>
        SelectedServerRunning = SelectedServer is not null && SelectedServer.Status != ServerStatus.Stopped;

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
    private async Task OpenGameSettingsAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        ActiveDetail = new GameSettingsViewModel(server);
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
    private async Task OpenModsAsync()
    {
        if (SelectedServer is not { } server) return;
        await GoHomeAsync();
        ActiveDetail = new ModsViewModel(server);
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

    // ── Auto-start ───────────────────────────────────────────────────────────

    private async Task AutoStartServersAsync()
    {
        // Brief pause so the UI is fully rendered before servers start
        await Task.Delay(1000);
        foreach (var server in Servers.Where(s => s.AutoStart))
        {
            // Reconcile actual process state — don't blindly start if already running
            var (status, _) = await _process.ReconcileStatusAsync(server);
            if (status != ServerStatus.Running && status != ServerStatus.Starting)
                _process.Start(server, out _);
        }
    }

    // ── Process commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private void StartServer()
    {
        if (SelectedServer is not { } server) return;
        ActionError = "";
        try
        {
            // ServerProcessService.Start sets server.Status itself, and Status now raises
            // PropertyChanged, so the list row re-renders on its own — no extra refresh needed.
            if (!_process.Start(server, out var err))
                ActionError = err;
        }
        catch (Exception ex) { ActionError = ex.Message; }
    }

    // Invoked from MainWindow's code-behind after the Stop dialog is confirmed, so the
    // chosen delay/message can be threaded through. Not a [RelayCommand] — the button
    // needs to show that dialog first, which needs a Window for Owner.
    public async Task StopServerAsync(int delaySeconds, string message)
    {
        if (SelectedServer is not { } server) return;
        ActionError = "";
        try
        {
            var progress = new Progress<string>(msg => ActionError = msg);
            await _process.StopAsync(server, progress, delaySeconds, message);
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

            if (!_process.Start(server, out var err))
            {
                ActionError = err;
                _restartingServers.Remove(server.Id);
            }
            else
            {
                ActionError = "";
            }
        }
        catch (Exception ex) { ActionError = ex.Message; _restartingServers.Remove(server.Id); }
    }

    // ── Install info ─────────────────────────────────────────────────────────

    public async Task RefreshInstallInfoAsync(Server? server)
    {
        if (server is null || string.IsNullOrWhiteSpace(server.InstallDir))
        {
            InstallBuildLabel  = "Not installed";
            InstallStatusLabel = "";
            InstallIsUpToDate  = false;
            return;
        }

        if (!_process.IsInstalled(server.InstallDir))
        {
            InstallBuildLabel  = "Not installed";
            InstallStatusLabel = "Click to install via SteamCMD";
            InstallIsUpToDate  = false;
            return;
        }

        var local = _steamCmd.GetInstallInfo(server.InstallDir);
        if (local is null)
        {
            InstallBuildLabel  = "Not installed";
            InstallStatusLabel = "Click to install via SteamCMD";
            InstallIsUpToDate  = false;
            return;
        }

        // Show local build immediately while the network check runs
        InstallBuildLabel  = $"Build {local.BuildId}";
        InstallStatusLabel = "Checking…";
        InstallIsUpToDate  = false;
        InstallCheckBusy   = true;

        try
        {
            var latest = await _steamCmd.GetLatestBuildIdAsync();
            if (latest is null)
            {
                // Couldn't determine the latest build (SteamCMD missing/offline) — don't claim
                // "up to date"; show a neutral state so we never hide a real update.
                InstallStatusLabel = "Version check unavailable";
                InstallIsUpToDate  = false;
                return;
            }

            InstallIsUpToDate  = local.BuildId == latest;
            InstallStatusLabel = InstallIsUpToDate
                ? "Up to date"
                : $"Update available ({latest})";
        }
        finally
        {
            InstallCheckBusy = false;
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

            // No collection mutation needed here any more — server.Status = status above
            // (line 404) already raises PropertyChanged, which is enough for the list row's
            // DataTriggers to re-render on their own. This used to also do
            // Servers.RemoveAt(idx) + Servers.Insert(idx, server) "to force a refresh", but that
            // clears the ListBox's SelectedItem the instant the item is removed — before the
            // Insert runs — which closed whatever detail card was open on every status change,
            // including the server simply finishing starting up.

            if (pendingError is not null)
                ActionError = pendingError;

            // Live stats transitions (selected server only)
            if (wasSelected)
            {
                UpdateSelectedServerRunning();

                if (status == ServerStatus.Running)
                    StartLiveStats(server);
                else if (status == ServerStatus.Stopped)
                    StopLiveStats();
            }
        }

        // Refresh process-level metrics every tick
        UpdateProcessMetrics();
    }

    // Called from MainWindow after install/update completes
    public async Task OnServerUpdatedAsync(Server server)
    {
        var disc = server.Discord ??= new Models.DiscordConfig();
        await SendDiscordEventAsync(server, disc.EventServerUpdated, "updated");
        await RefreshInstallInfoAsync(server);
    }

    private async Task SendDiscordEventAsync(Server server, Models.DiscordEventConfig evt, string key)
    {
        var disc = server.Discord;
        if (disc is null || !disc.Enabled) return;
        await _discord.SendEventAsync(evt, disc.DefaultWebhook,
            $"{server.Id}:{key}", server.Name);
    }

    // ── Live stats ───────────────────────────────────────────────────────────

    private void StartLiveStats(Server server)
    {
        StopLiveStats();

        if (server.LastPid is int pid)
        {
            try
            {
                _serverProcess = Process.GetProcessById(pid);
                LiveServerPid  = pid.ToString();
            }
            catch { _serverProcess = null; LiveServerPid = "—"; }
        }

        _miniPoller = new MetricsPoller(server);
        _miniPoller.Updated += snap => Application.Current.Dispatcher.Invoke(() =>
        {
            LivePlayers  = snap.Players;
            LiveFps      = snap.Fps;
            LiveEntities = snap.Entities;
            LiveGameTime = snap.GameTime;
        });
        _ = _miniPoller.StartAsync();
    }

    private void StopLiveStats()
    {
        if (_miniPoller is not null)
        {
            _ = _miniPoller.DisposeAsync().AsTask();
            _miniPoller = null;
        }
        _serverProcess   = null;
        LivePlayers      = 0;
        LiveFps          = 0;
        LiveEntities     = 0;
        LiveGameTime     = "—";
        LiveUptime       = "—";
        LiveServerPid    = "—";
        LiveCpuPct       = 0;
        LiveRamPct       = 0;
        LiveRamGbStr     = "—";
    }

    private void UpdateProcessMetrics()
    {
        // System-wide CPU (first call returns 0 — acceptable on cold start)
        try { LiveCpuPct = Math.Round(_cpuCounter.NextValue(), 1); }
        catch { LiveCpuPct = 0; }

        // System-wide RAM via GlobalMemoryStatusEx
        var mem = new MemStatus { Length = (uint)Marshal.SizeOf<MemStatus>() };
        if (GlobalMemoryStatusEx(ref mem))
        {
            LiveRamPct   = mem.MemoryLoad;
            var usedGb   = (mem.TotalPhys - mem.AvailPhys) / 1_073_741_824.0;
            LiveRamGbStr = $"{usedGb:F1} GB";
        }

        // Server process uptime (only while running)
        if (_serverProcess is not null)
        {
            try
            {
                _serverProcess.Refresh();
                var up     = DateTime.Now - _serverProcess.StartTime;
                LiveUptime = up.TotalDays >= 1
                    ? $"{(int)up.TotalDays}d {up.Hours:D2}h {up.Minutes:D2}m"
                    : $"{up.Hours:D2}:{up.Minutes:D2}:{up.Seconds:D2}";
            }
            catch { _serverProcess = null; LiveUptime = "—"; }
        }
    }

    // ── PInvoke: total system RAM ────────────────────────────────────────────

    [DllImport("kernel32.dll")]
    private static extern bool GlobalMemoryStatusEx(ref MemStatus stat);

    [StructLayout(LayoutKind.Sequential)]
    private struct MemStatus
    {
        public uint  Length;
        public uint  MemoryLoad;
        public ulong TotalPhys;
        public ulong AvailPhys;
        public ulong TotalPageFile;
        public ulong AvailPageFile;
        public ulong TotalVirtual;
        public ulong AvailVirtual;
        public ulong AvailExtendedVirtual;
    }
}
