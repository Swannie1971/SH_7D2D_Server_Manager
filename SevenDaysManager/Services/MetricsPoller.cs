using System.Text.RegularExpressions;
using System.Windows.Threading;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public sealed class MetricsPoller : IAsyncDisposable
{
    public record Snapshot(
        float   Fps,
        int     HeapUsedMb,
        int     HeapMaxMb,
        int     RssMb,
        int     Chunks,
        int     Cgo,
        int     Players,
        int     Zombies,
        int     Entities,
        int     Items,
        string  GameTime,
        string  ServerVersion);

    // Each field parsed independently — one missing/renamed field won't break the rest
    private static readonly Regex FpsRx    = new(@"FPS:\s*([\d.]+)",          RegexOptions.Compiled);
    private static readonly Regex HeapRx   = new(@"Heap:\s*([\d.]+)/([\d.]+)\s*MB", RegexOptions.Compiled);
    private static readonly Regex RssRx    = new(@"RSS:\s*([\d.]+)\s*MB",          RegexOptions.Compiled);
    private static readonly Regex ChunksRx = new(@"Chunks:\s*(\d+)",         RegexOptions.Compiled);
    private static readonly Regex CgoRx    = new(@"CGO:\s*(\d+)",            RegexOptions.Compiled);
    private static readonly Regex PlyRx    = new(@"Ply:\s*(\d+)",            RegexOptions.Compiled);
    private static readonly Regex ZomRx    = new(@"Zom:\s*(\d+)",            RegexOptions.Compiled);
    private static readonly Regex EntRx    = new(@"Ent:\s*(\d+)",            RegexOptions.Compiled);
    private static readonly Regex ItemsRx  = new(@"Items:\s*(\d+)",          RegexOptions.Compiled);

    private static readonly Regex TimeRx    = new(@"Day\s+(\d+),\s*([\d:]+)", RegexOptions.Compiled);
    private static readonly Regex VersionRx = new(@"Game version:\s*(.+?)(?:\s+Compatibility|$)", RegexOptions.Compiled);

    private readonly Server         _server;
    private readonly TelnetClient   _telnet = new();
    private readonly DispatcherTimer _timer;
    private string _gameTime = "—";
    private string _version  = "—";

    public Snapshot? Latest { get; private set; }
    public event Action<Snapshot>? Updated;
    public event Action? Connected;
    public event Action? Disconnected;

    public MetricsPoller(Server server)
    {
        _server = server;
        _timer  = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        _timer.Tick += async (_, _) => await RequestAsync();
        _telnet.LineReceived += ParseLine;
        _telnet.Disconnected += OnDisconnected;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        if (await _telnet.ConnectAsync("127.0.0.1", _server.TelnetPort, _server.TelnetPassword, ct))
        {
            Connected?.Invoke();
            _timer.Start();
            await RequestAsync();
        }
    }

    private async Task RequestAsync()
    {
        if (!_telnet.IsConnected) return;
        await _telnet.SendAsync("mem");
        await _telnet.SendAsync("gettime");
        await _telnet.SendAsync("version");
    }

    private void ParseLine(string line)
    {
        // Mem stats line is identified by presence of FPS:
        var fps = FpsRx.Match(line);
        if (fps.Success)
        {
            var heap  = HeapRx.Match(line);
            var rss   = RssRx.Match(line);
            var chunk = ChunksRx.Match(line);
            var cgo   = CgoRx.Match(line);
            var ply   = PlyRx.Match(line);
            var zom   = ZomRx.Match(line);
            var ent   = EntRx.Match(line);
            var items = ItemsRx.Match(line);

            var prev = Latest;
            Latest = new Snapshot(
                Fps:           float.Parse(fps.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture),
                HeapUsedMb:   heap.Success  ? (int)float.Parse(heap.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)  : prev?.HeapUsedMb  ?? 0,
                HeapMaxMb:    heap.Success  ? (int)float.Parse(heap.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture)  : prev?.HeapMaxMb   ?? 100,
                RssMb:        rss.Success   ? (int)float.Parse(rss.Groups[1].Value,  System.Globalization.CultureInfo.InvariantCulture)  : prev?.RssMb       ?? 0,
                Chunks:       chunk.Success ? int.Parse(chunk.Groups[1].Value) : prev?.Chunks      ?? 0,
                Cgo:          cgo.Success   ? int.Parse(cgo.Groups[1].Value)   : prev?.Cgo         ?? 0,
                Players:      ply.Success   ? int.Parse(ply.Groups[1].Value)   : prev?.Players     ?? 0,
                Zombies:      zom.Success   ? int.Parse(zom.Groups[1].Value)   : prev?.Zombies     ?? 0,
                Entities:     ent.Success   ? int.Parse(ent.Groups[1].Value)   : prev?.Entities    ?? 0,
                Items:        items.Success ? int.Parse(items.Groups[1].Value) : prev?.Items       ?? 0,
                GameTime:     _gameTime,
                ServerVersion: _version);
            Updated?.Invoke(Latest);
            return;
        }

        var t = TimeRx.Match(line);
        if (t.Success)
        {
            _gameTime = $"Day {t.Groups[1].Value}, {t.Groups[2].Value}";
            return;
        }

        var v = VersionRx.Match(line);
        if (v.Success)
            _version = v.Groups[1].Value.Trim();
    }

    private void OnDisconnected()
    {
        _timer.Stop();
        Disconnected?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _timer.Stop();
        await _telnet.DisposeAsync();
    }
}
