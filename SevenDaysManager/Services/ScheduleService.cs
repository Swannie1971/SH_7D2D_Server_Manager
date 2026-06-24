using System.Windows;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public class ScheduleService : IDisposable
{
    private readonly Server          _server;
    private readonly DiscordService  _discord = new();
    private readonly Func<Task>      _stopServer;
    private readonly Func<Task>      _startServer;

    private System.Threading.Timer?  _timer;
    private readonly HashSet<int>    _warnsSent = new();
    private bool                     _restarting;

    public DateTime?       NextRestartAt { get; private set; }
    public ScheduleConfig  Config        { get; private set; } = new();

    public event Action<string>? LogEntry;   // activity log line
    public event Action?         StateChanged;

    public ScheduleService(Server server, Func<Task> stopServer, Func<Task> startServer)
    {
        _server      = server;
        _stopServer  = stopServer;
        _startServer = startServer;
    }

    // ── Apply / start / stop ─────────────────────────────────────────────────

    public void Apply(ScheduleConfig config)
    {
        Config = config;

        if (!config.Enabled)
        {
            NextRestartAt = null;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
            NotifyState();
            return;
        }

        _warnsSent.Clear();
        _restarting   = false;
        NextRestartAt = CalcNext(config);
        Log($"Schedule enabled — next restart at {NextRestartAt:HH:mm:ss}");

        _timer ??= new System.Threading.Timer(Tick, null, Timeout.Infinite, Timeout.Infinite);
        _timer.Change(TimeSpan.Zero, TimeSpan.FromSeconds(30));
    }

    public void Disable()
    {
        Config.Enabled = false;
        NextRestartAt  = null;
        _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        NotifyState();
    }

    // ── Manual trigger ───────────────────────────────────────────────────────

    public async Task TriggerRestartNowAsync()
    {
        if (_restarting) return;
        Log("Manual restart triggered.");
        await DoRestartAsync();
    }

    // ── Timer tick ───────────────────────────────────────────────────────────

    private void Tick(object? _)
    {
        if (!Config.Enabled || NextRestartAt is null || _restarting) return;

        var remaining = NextRestartAt.Value - DateTime.Now;

        // Send due warnings
        foreach (var warnMin in Config.WarnMinutes)
        {
            if (_warnsSent.Contains(warnMin)) continue;
            if (remaining.TotalMinutes <= warnMin + 0.5 && remaining.TotalMinutes > warnMin - 0.5)
            {
                _warnsSent.Add(warnMin);
                _ = SendWarningAsync(warnMin);
            }
        }

        // Time to restart?
        if (remaining.TotalSeconds <= 15)
            _ = DoRestartAsync();

        NotifyState();
    }

    private async Task SendWarningAsync(int minutes)
    {
        Log($"Warning: restarting in {minutes} min.");

        var inGame = Config.InGameWarning.Replace("{minutes}", minutes.ToString());
        await SendInGameAsync(inGame);

        var disc = _server.Discord;
        if (disc?.Enabled == true)
            await _discord.SendEventAsync(disc.EventRestartWarn, disc.DefaultWebhook,
                $"{_server.Id}:warn:{minutes}", _server.Name, minutes: minutes);
    }

    private async Task DoRestartAsync()
    {
        if (_restarting) return;
        _restarting = true;

        Log("Sending restart message…");
        await SendInGameAsync(Config.InGameNow);

        // Restart Discord notification is fired by MainViewModel when the server comes back online
        // (distinguishes restart from plain start via _restartingServers HashSet)

        await Task.Delay(2000);

        Log("Stopping server…");
        try { await _stopServer(); } catch (Exception ex) { Log($"Stop error: {ex.Message}"); }

        await Task.Delay(5000);

        Log("Starting server…");
        try { await _startServer(); } catch (Exception ex) { Log($"Start error: {ex.Message}"); }

        _warnsSent.Clear();
        _restarting   = false;
        NextRestartAt = CalcNext(Config);
        Log($"Next restart at {NextRestartAt:HH:mm:ss}");
        NotifyState();
    }

    // ── In-game say ──────────────────────────────────────────────────────────

    private async Task SendInGameAsync(string message)
    {
        if (_server.Status != ServerStatus.Running) return;
        try
        {
            var tel = new TelnetClient();
            var host = string.IsNullOrWhiteSpace(_server.ServerIp) ? "127.0.0.1" : _server.ServerIp;
            await tel.ConnectAsync(host, _server.TelnetPort, _server.TelnetPassword);
            await tel.SendAsync($"say \"{message}\"");
        }
        catch { }
    }


    // ── Helpers ──────────────────────────────────────────────────────────────

    private static DateTime CalcNext(ScheduleConfig config)
    {
        if (config.Mode == ScheduleMode.Interval)
            return DateTime.Now.AddHours(config.IntervalHours);

        // Daily mode — find next upcoming time
        var now   = DateTime.Now;
        var today = DateOnly.FromDateTime(now);
        var times = config.DailyTimes
            .Select(t => TimeOnly.TryParse(t, out var to) ? to : (TimeOnly?)null)
            .Where(t => t.HasValue)
            .Select(t => today.ToDateTime(t!.Value))
            .Select(dt => dt <= now ? dt.AddDays(1) : dt)
            .OrderBy(dt => dt)
            .ToList();

        return times.Count > 0 ? times[0] : DateTime.Now.AddHours(6);
    }

    private void Log(string msg)
    {
        var line = $"[{DateTime.Now:HH:mm:ss}] {msg}";
        Application.Current?.Dispatcher.Invoke(() => LogEntry?.Invoke(line));
    }

    private void NotifyState() =>
        Application.Current?.Dispatcher.Invoke(() => StateChanged?.Invoke());

    public void Dispose() => _timer?.Dispose();
}
