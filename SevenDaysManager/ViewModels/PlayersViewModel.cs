using System.Collections.ObjectModel;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class PlayersViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Server        _server;
    private readonly TelnetClient  _telnet = new();
    private readonly DispatcherTimer _refreshTimer;

    // lp parse state
    private bool           _collectingLp;
    private int            _expectedCount;
    private readonly List<PlayerInfo> _buffer = new();

    private static readonly Regex TotalRx    = new(@"Total of (\d+) in the game", RegexOptions.Compiled);
    // Matches the start of a player line: "1. id=171, PlayerName, pos="
    private static readonly Regex PlayerLineRx = new(@"\d+\.\s*id=(\d+),\s*(.+?),\s*pos=", RegexOptions.Compiled);
    private static readonly Regex SteamIdRx  = new(@"steamid=(\S+)",      RegexOptions.Compiled);
    private static readonly Regex IpRx       = new(@"\bip=([^,\s]+)",     RegexOptions.Compiled);

    public ObservableCollection<PlayerInfo> Players { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPlayerSelected))]
    private PlayerInfo? _selectedPlayer;

    [ObservableProperty] private bool   _isConnected;
    [ObservableProperty] private string _broadcastMessage = "";
    [ObservableProperty] private string _actionReason     = "";
    [ObservableProperty] private string _statusText       = "Connecting…";
    [ObservableProperty] private int    _playerCount;
    [ObservableProperty] private string _lastRefreshed    = "—";

    public bool IsOffline        => !IsConnected;
    public bool IsPlayerSelected => SelectedPlayer is not null;

    public string ServerName => _server.Name;

    public PlayersViewModel(Server server)
    {
        _server = server;
        _telnet.LineReceived += OnLine;
        _telnet.Disconnected += OnDisconnected;

        _refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _refreshTimer.Tick += async (_, _) => await RequestPlayersAsync();
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        var host = string.IsNullOrWhiteSpace(_server.ServerIp) ? "127.0.0.1" : _server.ServerIp;
        var ok = await _telnet.ConnectAsync(host, _server.TelnetPort, _server.TelnetPassword, ct);
        if (ok)
        {
            IsConnected = true;
            StatusText  = "Connected";
            _refreshTimer.Start();
            await RequestPlayersAsync();
        }
        else
        {
            StatusText = "Could not connect — check Telnet port/password in Server Settings";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await RequestPlayersAsync();

    [RelayCommand(CanExecute = nameof(CanBroadcast))]
    private async Task BroadcastAsync()
    {
        var msg = BroadcastMessage.Trim();
        if (string.IsNullOrEmpty(msg)) return;
        await _telnet.SendAsync($"say \"{SanitizeTelnet(msg)}\"");
        BroadcastMessage = "";
    }
    private bool CanBroadcast() => IsConnected && !string.IsNullOrWhiteSpace(BroadcastMessage);

    partial void OnBroadcastMessageChanged(string value) => BroadcastCommand.NotifyCanExecuteChanged();

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsOffline));
        BroadcastCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(IsPlayerSelected))]
    private async Task KickAsync()
    {
        if (SelectedPlayer is not { } p) return;
        var reason = SanitizeTelnet(string.IsNullOrWhiteSpace(ActionReason) ? "Kicked by admin" : ActionReason.Trim());
        await _telnet.SendAsync($"kick {p.EntityId} \"{reason}\"");
        ActionReason = "";
        await Task.Delay(500);
        await RequestPlayersAsync();
    }

    [RelayCommand(CanExecute = nameof(IsPlayerSelected))]
    private async Task BanAsync()
    {
        if (SelectedPlayer is not { } p) return;
        var reason = SanitizeTelnet(string.IsNullOrWhiteSpace(ActionReason) ? "Banned by admin" : ActionReason.Trim());
        await _telnet.SendAsync($"ban add {p.SteamId} years 1 \"{reason}\"");
        await _telnet.SendAsync($"kick {p.EntityId} \"{reason}\"");
        ActionReason = "";
        await Task.Delay(500);
        await RequestPlayersAsync();
    }

    partial void OnSelectedPlayerChanged(PlayerInfo? value)
    {
        KickCommand.NotifyCanExecuteChanged();
        BanCommand.NotifyCanExecuteChanged();
        ActionReason = "";
    }

    private async Task RequestPlayersAsync()
    {
        if (!_telnet.IsConnected) return;
        _buffer.Clear();
        _collectingLp = false;
        await _telnet.SendAsync("lp");
    }

    private void OnLine(string line)
    {
        var total = TotalRx.Match(line);
        if (total.Success)
        {
            _buffer.Clear();
            _expectedCount = int.Parse(total.Groups[1].Value);
            _collectingLp  = _expectedCount > 0;

            if (_expectedCount == 0)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Players.Clear();
                    PlayerCount   = 0;
                    LastRefreshed = DateTime.Now.ToString("HH:mm:ss");
                });
            }
            return;
        }

        if (!_collectingLp) return;

        var header = PlayerLineRx.Match(line);
        if (!header.Success) return;

        var steamM = SteamIdRx.Match(line);
        var ipM    = IpRx.Match(line);

        _buffer.Add(new PlayerInfo
        {
            EntityId = int.Parse(header.Groups[1].Value),
            Name     = header.Groups[2].Value.Trim(),
            Health   = ParseIntField(line, "health"),
            Deaths   = ParseIntField(line, "deaths"),
            Zombies  = ParseIntField(line, "zombies"),
            Score    = ParseIntField(line, "score"),
            Level    = ParseIntField(line, "level"),
            Ping     = ParseIntField(line, "ping"),
            SteamId  = steamM.Success ? steamM.Groups[1].Value.Trim() : "",
            Ip       = ipM.Success    ? ipM.Groups[1].Value.Trim()    : "",
        });

        if (_buffer.Count < _expectedCount) return;

        _collectingLp = false;
        var captured = _buffer.ToList();
        Application.Current.Dispatcher.Invoke(() =>
        {
            Players.Clear();
            foreach (var p in captured) Players.Add(p);
            PlayerCount   = Players.Count;
            LastRefreshed = DateTime.Now.ToString("HH:mm:ss");
        });
    }

    private void OnDisconnected()
    {
        IsConnected = false;
        StatusText  = "Disconnected";
        _refreshTimer.Stop();
    }

    public async ValueTask DisposeAsync()
    {
        _refreshTimer.Stop();
        await _telnet.DisposeAsync();
    }

    private static string SanitizeTelnet(string s) =>
        s.Replace("\"", "'").Replace("\r", "").Replace("\n", " ");

    // Extract a numeric field by name regardless of its position in the lp line
    private static int ParseIntField(string line, string key)
    {
        var m = Regex.Match(line, $@"\b{key}=(-?\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }
}
