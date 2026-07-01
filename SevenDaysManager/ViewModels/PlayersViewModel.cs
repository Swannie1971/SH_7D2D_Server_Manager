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

    // lp parse state — player rows stream in BEFORE the "Total of N" footer,
    // so we collect every row as it arrives and commit when the footer lands.
    private readonly List<PlayerInfo> _buffer = new();

    // Footer line that ends the list: "Total of 1 in the game" (wording varies by version)
    private static readonly Regex TotalRx      = new(@"Total of (\d+)",                      RegexOptions.Compiled);
    // A player row: "0. id=171, PlayerName, pos=" — leading index/spacing varies by version
    private static readonly Regex PlayerLineRx = new(@"\d+\.\s*id=(\d+),\s*(.+?),\s*pos=",  RegexOptions.Compiled);
    // Newer builds use "pltfmid=Steam_7656..."; older builds used "steamid=7656..."
    private static readonly Regex SteamIdRx     = new(@"(?:pltfmid|steamid)=([^,\s]+)",      RegexOptions.Compiled);
    private static readonly Regex CrossIdRx     = new(@"crossid=([^,\s]+)",                  RegexOptions.Compiled);
    private static readonly Regex IpRx          = new(@"\bip=([^,\s]+)",                     RegexOptions.Compiled);
    private static readonly Regex PosRx         = new(@"pos=\(([^)]*)\)",                    RegexOptions.Compiled);
    private static readonly Regex RemoteRx      = new(@"remote=(True|False)",                RegexOptions.Compiled | RegexOptions.IgnoreCase);

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

    // Level 0 = full admin/owner in 7D2D; higher numbers map to narrower permission tiers
    // defined in the server's permission_admin.xml (or config panel for managed hosts).
    public async Task<bool> SetAdminLevelAsync(int entityId, int level)
    {
        if (!_telnet.IsConnected) return false;
        await _telnet.SendAsync($"admin add {entityId} {level}");
        return true;
    }

    public async Task<bool> RemoveAdminAsync(int entityId)
    {
        if (!_telnet.IsConnected) return false;
        await _telnet.SendAsync($"admin remove {entityId}");
        return true;
    }

    private async Task RequestPlayersAsync()
    {
        if (!_telnet.IsConnected) return;
        _buffer.Clear();
        await _telnet.SendAsync("lp");
    }

    private void OnLine(string line)
    {
        // Player rows arrive first, one per line, then a "Total of N" footer ends the list.
        var header = PlayerLineRx.Match(line);
        if (header.Success)
        {
            var steamM = SteamIdRx.Match(line);
            var crossM = CrossIdRx.Match(line);
            var ipM    = IpRx.Match(line);
            var remoteM = RemoteRx.Match(line);

            _buffer.Add(new PlayerInfo
            {
                EntityId    = int.Parse(header.Groups[1].Value),
                Name        = header.Groups[2].Value.Trim(),
                Health      = ParseIntField(line, "health"),
                Deaths      = ParseIntField(line, "deaths"),
                Zombies     = ParseIntField(line, "zombies"),
                PlayerKills = ParseIntField(line, "players"),
                Score       = ParseIntField(line, "score"),
                Level       = ParseIntField(line, "level"),
                Ping        = ParseIntField(line, "ping"),
                SteamId     = steamM.Success  ? steamM.Groups[1].Value.Trim()  : "",
                CrossId     = crossM.Success  ? crossM.Groups[1].Value.Trim()  : "",
                Ip          = ipM.Success     ? ipM.Groups[1].Value.Trim()     : "",
                Position    = FormatPos(PosRx.Match(line)),
                Remote      = remoteM.Success && remoteM.Groups[1].Value.Equals("True", StringComparison.OrdinalIgnoreCase),
            });
            return;
        }

        // Footer: commit whatever rows we collected (handles 0 players too).
        if (TotalRx.IsMatch(line))
        {
            CommitBuffer();
            _buffer.Clear();
        }
    }

    private void CommitBuffer()
    {
        var captured = _buffer.ToList();
        Application.Current.Dispatcher.Invoke(() =>
        {
            // Remember the current selection so the action panel stays open across refreshes
            var selectedId = SelectedPlayer?.EntityId;

            Players.Clear();
            foreach (var p in captured) Players.Add(p);
            PlayerCount   = Players.Count;
            LastRefreshed = DateTime.Now.ToString("HH:mm:ss");

            // Re-select the same player (by entity id) if they're still online
            if (selectedId is int id)
                SelectedPlayer = Players.FirstOrDefault(p => p.EntityId == id);
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

    // "pos=(-101.3, 48.8, -285.6)" -> "-101 49 -286" (rounded whole coords)
    private static string FormatPos(Match posMatch)
    {
        if (!posMatch.Success) return "";
        var parts = posMatch.Groups[1].Value.Split(',');
        var coords = parts.Select(s =>
            float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var f)
                ? ((int)Math.Round(f)).ToString()
                : s.Trim());
        return string.Join(" ", coords);
    }
}
