using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Threading;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

/// <summary>
/// Feeds the live-stats panels (FPS, heap, players, entities, game time).
///
/// <para><b>Stats come from the server's LOG, not from a telnet query.</b> The obvious approach —
/// and what this class used to do — is to send the <c>mem</c> console command every few seconds.
/// Do not do that: in 7D2D <c>mem</c> forces a full asset unload and GC mark, which is
/// stop-the-world. Two real servers, measured with no players connected, stalled ~350 ms
/// (Ryzen 7600) and ~810 ms (Xeon E5-2690 v4) on <i>every</i> call. With this poller running on a
/// 5 s timer — and a second one stacked on top when the Overview card was open — that produced
/// exactly the "random lag spikes" players were reporting.</para>
///
/// <para>The server already writes a line carrying all the same numbers to its log on its own
/// schedule, so we tail that instead: identical data, zero cost. Only the genuinely cheap
/// <c>gettime</c>/<c>version</c> commands still go over telnet.</para>
/// </summary>
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

    // ⚠ Two different shapes for the same data, because we no longer ask for it:
    //   'mem' printed   "Heap: 956.4/1657.1MB"
    //   the server's own periodic line prints  "Heap: 956.4MB Max: 1657.1MB"
    // We now read the latter (see the class comment), so Max is a SEPARATE field. Matching
    // only the old "used/max" form would silently leave the heap gauge reading zero.
    private static readonly Regex HeapRx    = new(@"Heap:\s*([\d.]+)\s*MB",   RegexOptions.Compiled);
    private static readonly Regex HeapMaxRx = new(@"Max:\s*([\d.]+)\s*MB",    RegexOptions.Compiled);
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

        // ⚠ Do NOT send "mem" here. It looks like a harmless stats query, but in 7D2D it forces
        // a full asset unload + GC mark, and that is stop-the-world: the server freezes for the
        // duration. Measured on two real servers, 5 s apart, with nobody even connected:
        //     Ryzen 7600  -> ~350 ms per call
        //     Xeon E5-2690 v4 -> ~810 ms per call   (MarkObjects is single-threaded, so his
        //                                            56 cores bought him nothing)
        // That WAS the "random lag spikes" players reported. The stats it returned are printed
        // by the server to its own log for free — see ReadLogStats. Never reintroduce it.
        await _telnet.SendAsync("gettime");

        // The game version cannot change while the server is up, so ask once and stop. We used
        // to re-request it every tick, which did nothing but double this poller's footprint in
        // the server log (a tester noticed the noise).
        if (!_askedVersion)
        {
            _askedVersion = true;
            await _telnet.SendAsync("version");
        }

        ReadLogStats();
    }

    // Version is fetched once per connection — see RequestAsync. Reset on disconnect so a
    // reconnect (or a server restart on a new build) picks the new one up.
    private bool _askedVersion;

    // ── Stats from the log (free) ─────────────────────────────────────────────

    // Byte offset we've already consumed, so each tick only reads what's new.
    // -1 = we haven't attached yet; the first read seeks to the END so we don't churn through
    // a multi-megabyte backlog and report stats from a previous session as if they were live.
    private long _logOffset = -1;

    /// <summary>
    /// Pull the newest server-printed stats line out of the log. The server emits
    ///   "Time: 21.37m FPS: 18.31 Heap: 956.4MB Max: 1657.1MB Chunks: 27 ... RSS: 2922.0MB"
    /// on its own schedule, containing every field the old "mem" command returned — at no cost
    /// to the server, because it was going to write that line anyway.
    /// </summary>
    private void ReadLogStats()
    {
        var path = _server.ServerLogPath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            path = Path.Combine(_server.InstallDir ?? "", "manager_server.log");
            if (!File.Exists(path)) return;
        }

        try
        {
            // ReadWrite share: the server holds this file open for writing.
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // First attach: jump to the end. We want live stats, not the whole backlog.
            if (_logOffset < 0) _logOffset = fs.Length;

            // Log rotated/truncated (server restarted) — start over rather than seek past the end.
            if (_logOffset > fs.Length) _logOffset = 0;

            fs.Seek(_logOffset, SeekOrigin.Begin);
            using var sr = new StreamReader(fs);

            string? line;
            while ((line = sr.ReadLine()) is not null)
                ParseLine(line);

            _logOffset = fs.Position;
        }
        catch
        {
            // Log unreadable this tick (rotation, lock) — just try again on the next one.
        }
    }

    private void ParseLine(string line)
    {
        // Mem stats line is identified by presence of FPS:
        var fps = FpsRx.Match(line);
        if (fps.Success)
        {
            var heap    = HeapRx.Match(line);
            var heapMax = HeapMaxRx.Match(line);
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
                HeapUsedMb:   heap.Success    ? (int)float.Parse(heap.Groups[1].Value,    System.Globalization.CultureInfo.InvariantCulture) : prev?.HeapUsedMb ?? 0,
                HeapMaxMb:    heapMax.Success ? (int)float.Parse(heapMax.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : prev?.HeapMaxMb  ?? 100,
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
        // Re-ask for the version on the next connection: the server may come back on a new build.
        _askedVersion = false;
        Disconnected?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        _timer.Stop();
        await _telnet.DisposeAsync();
    }
}
