using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Text;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public class ServerProcessService
{
    private static readonly ServerConfigService ConfigService = new();

    public const string ServerExeName = "7DaysToDieServer.exe";

    // Set by MonitorStartupAsync (background) when it sees the LogOn line.
    // Read by ReconcileStatusAsync (UI timer) so the Starting→Running transition
    // flows through PollStatus's change-detection instead of being mutated directly.
    private readonly ConcurrentDictionary<string, byte> _readyServers = new();

    // ── Start ─────────────────────────────────────────────────────────────────

    public bool Start(Server server, out string error)
    {
        error = "";

        if (string.IsNullOrWhiteSpace(server.InstallDir))
        {
            error = "Install directory is not set.";
            return false;
        }

        var exe = Path.Combine(server.InstallDir, ServerExeName);
        if (!File.Exists(exe))
        {
            error = "Server executable not found — install the server first.";
            return false;
        }

        var logPath = Path.Combine(server.InstallDir, "manager_server.log");

        var psi = new ProcessStartInfo
        {
            FileName         = exe,
            WorkingDirectory = server.InstallDir,
            UseShellExecute  = false,
            CreateNoWindow   = true
        };
        psi.ArgumentList.Add("-batchmode");
        psi.ArgumentList.Add("-nographics");
        psi.ArgumentList.Add("-dedicated");
        psi.ArgumentList.Add("-nosteam");
        psi.ArgumentList.Add("-configfile=serverconfig.xml");
        psi.ArgumentList.Add("-logfile");
        psi.ArgumentList.Add(logPath);

        try
        {
            ConfigService.WriteConfig(server);

            var process = Process.Start(psi) ?? throw new Exception("Process.Start returned null.");
            server.LastPid         = process.Id;
            server.Status          = ServerStatus.Starting;
            server.ExitCode        = null;
            server.ServerLogPath   = null;
            server.ServerStartTime = DateTime.UtcNow;
            _readyServers.TryRemove(server.Id, out _);
            App.DataStore.SaveServer(server);

            // Background: connect to Telnet as soon as it opens, stream until LogOn
            _ = MonitorStartupAsync(server);

            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    // ── Stop (graceful via Telnet → force kill fallback) ─────────────────────

    public async Task StopAsync(Server server, IProgress<string> progress)
    {
        server.Status = ServerStatus.Stopping;

        var graceful = await TryGracefulShutdownAsync(server, progress);
        if (!graceful)
        {
            progress.Report("Telnet unavailable — force stopping…");
            ForceKill(server);
        }

        server.LastPid = null;
        server.Status  = ServerStatus.Stopped;
        _readyServers.TryRemove(server.Id, out _);
        App.DataStore.SaveServer(server);
    }

    private async Task<bool> TryGracefulShutdownAsync(Server server, IProgress<string> progress)
    {
        if (server.LastPid is not { } pid) return false;
        if (server.TelnetPort <= 0)        return false;

        Process? proc = null;
        try { proc = Process.GetProcessById(pid); }
        catch { return false; }
        if (proc.HasExited) return false;

        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await tcp.ConnectAsync("127.0.0.1", server.TelnetPort, cts.Token);

            var stream = tcp.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8);
            var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true, NewLine = "\n" };

            // Drain initial banner / password prompt
            await DrainAsync(stream, milliseconds: 800);

            // Authenticate if password set
            if (!string.IsNullOrEmpty(server.TelnetPassword))
            {
                await writer.WriteLineAsync(server.TelnetPassword);
                await DrainAsync(stream, milliseconds: 600);
            }

            // Save world
            progress.Report("Saving world…");
            await writer.WriteLineAsync("saveworld");
            await Task.Delay(8000); // give server time to write all chunks

            // Initiate shutdown
            progress.Report("Shutting down server…");
            await writer.WriteLineAsync("shutdown");

            // Wait up to 30 s for clean exit
            var exited = await proc.WaitForExitAsync(TimeSpan.FromSeconds(30));
            if (!exited)
            {
                progress.Report("Shutdown timed out — force killing…");
                ForceKill(server);
            }

            return true;
        }
        catch
        {
            // Telnet failed — fall through to force kill
            return false;
        }
    }

    private static async Task DrainAsync(NetworkStream stream, int milliseconds)
    {
        var buf = new byte[4096];
        var deadline = DateTime.UtcNow.AddMilliseconds(milliseconds);
        while (DateTime.UtcNow < deadline)
        {
            if (stream.DataAvailable)
                await stream.ReadAsync(buf);
            else
                await Task.Delay(50);
        }
    }

    private static void ForceKill(Server server)
    {
        if (server.LastPid is not { } pid) return;
        try
        {
            var p = Process.GetProcessById(pid);
            if (!p.HasExited) p.Kill(entireProcessTree: true);
        }
        catch { /* already gone */ }
    }

    // ── Restart ───────────────────────────────────────────────────────────────

    public async Task RestartAsync(Server server, IProgress<string> progress)
    {
        await StopAsync(server, progress);
    }

    // ── Status reconciliation (call on a timer) ───────────────────────────────

    // MonitorStartupAsync: connects to Telnet as soon as it opens and streams
    // output until it sees "GameServer.LogOn successful", then marks Running.
    // Runs entirely in the background — the status timer picks up the change.
    private async Task MonitorStartupAsync(Server server)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(10));

        // 1. Wait for the Telnet port to open (polls every 2 s)
        while (!cts.IsCancellationRequested && IsProcessAlive(server.LastPid))
        {
            await Task.Delay(2000, cts.Token).ConfigureAwait(false);
            try
            {
                using var probe = new TcpClient();
                using var probeCts = new CancellationTokenSource(500);
                await probe.ConnectAsync("127.0.0.1", server.TelnetPort, probeCts.Token);
                break; // port is open
            }
            catch { /* not open yet */ }
        }

        if (!IsProcessAlive(server.LastPid)) return;

        // 2. Use TelnetClient — it handles negotiation bytes, banner drain, and auth
        await using var client = new TelnetClient();
        var ready = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        client.LineReceived += line =>
        {
            if (line.Contains("GameServer.LogOn successful", StringComparison.OrdinalIgnoreCase))
                ready.TrySetResult(true);
            else if (line.Contains("SteamGameServer_Init failed", StringComparison.OrdinalIgnoreCase) ||
                     line.Contains("Steam initialization failed", StringComparison.OrdinalIgnoreCase))
                ready.TrySetResult(false);
        };
        client.Disconnected += () => ready.TrySetResult(false);

        var connected = await client.ConnectAsync("127.0.0.1", server.TelnetPort,
                                                   server.TelnetPassword, cts.Token);
        if (!connected) return;

        // Signal readiness via the flag — do NOT mutate server.Status here, or
        // PollStatus's change-detection would see no diff and never refresh the UI.
        if (await ready.Task.WaitAsync(cts.Token) == true)
            _readyServers[server.Id] = 1;
    }

    private static bool IsProcessAlive(int? pid)
    {
        if (pid is not { } p) return false;
        try { return !Process.GetProcessById(p).HasExited; }
        catch { return false; }
    }

    public async Task<(string Status, string? LastLogLines)> ReconcileStatusAsync(Server server)
    {
        if (server.LastPid is not { } pid)
            return (ServerStatus.Stopped, null);

        try
        {
            var p = Process.GetProcessById(pid);
            if (p.HasExited)
            {
                server.ExitCode = p.ExitCode;
                _readyServers.TryRemove(server.Id, out _);
                return (ServerStatus.Stopped, ReadLogTail(server));
            }

            // Monitor saw the LogOn line → report Running. server.Status is still
            // "starting" at this point, so PollStatus detects the diff and refreshes.
            if (server.Status == ServerStatus.Starting && _readyServers.ContainsKey(server.Id))
                return (ServerStatus.Running, null);

            // Fallback: still Starting after 3 min (e.g. app restarted while the
            // server was already up, so the monitor missed the line). Probe Telnet.
            if (server.Status == ServerStatus.Starting
                && server.ServerStartTime is { } t
                && (DateTime.Now - t).TotalMinutes > 3
                && await IsTelnetResponsiveAsync(server))
            {
                return (ServerStatus.Running, null);
            }

            return (server.Status, null);
        }
        catch
        {
            return (ServerStatus.Stopped, ReadLogTail(server));
        }
    }

    private static async Task<bool> IsTelnetResponsiveAsync(Server server)
    {
        if (server.TelnetPort <= 0) return false;
        try
        {
            using var tcp = new TcpClient();
            using var cts = new CancellationTokenSource(2000);
            await tcp.ConnectAsync("127.0.0.1", server.TelnetPort, cts.Token);
            var stream = tcp.GetStream();
            using var reader = new StreamReader(stream, Encoding.UTF8);
            using var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = true };

            await Task.Delay(600, cts.Token);
            if (!string.IsNullOrEmpty(server.TelnetPassword))
            {
                await writer.WriteLineAsync(server.TelnetPassword);
                await Task.Delay(400, cts.Token);
            }

            await writer.WriteLineAsync("version");
            // Read up to 10 lines looking for "Game Version"
            for (var i = 0; i < 10; i++)
            {
                var line = await reader.ReadLineAsync(cts.Token);
                if (line is null) break;
                if (line.Contains("Game Version", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
        catch { return false; }
    }

    // GPU/shader errors are normal for headless Unity servers — not the cause of shutdown
    private static readonly string[] KnownNoise =
    [
        "shader is not supported on this GPU",
        "Fallback handler could not load library",
        "ALLOC_TYPETREE",
        "Peak usage frame",
        "Requested Block Size",
        "Peak Block count",
        "Peak Allocated memory",
        "Peak Large allocation"
    ];

    private static string? ReadLogTail(Server server)
    {
        // Try cached path, then discover from common 7D2D locations
        var path = server.ServerLogPath;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            var dirs = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "7DaysToDie", "logs"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "The Fun Pimps", "7 Days to Die Dedicated Server"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "The Fun Pimps", "7 Days to Die"),
                server.InstallDir ?? "",
            };
            var cutoff = (server.ServerStartTime ?? DateTime.MinValue).AddSeconds(-10);
            path = dirs.Where(Directory.Exists)
                       .SelectMany(d => Directory.EnumerateFiles(d, "*.txt").Concat(Directory.EnumerateFiles(d, "*.log")))
                       .Select(f => new FileInfo(f))
                       .Where(f => f.CreationTime >= cutoff)
                       .OrderByDescending(f => f.CreationTime)
                       .FirstOrDefault()?.FullName;
        }
        return ReadLogTail(path, 15);
    }

    private static string? ReadLogTail(string? path, int lines)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
        try
        {
            var all = File.ReadAllLines(path);

            // Real errors: exclude known GPU/memory noise
            var realErrors = all
                .Where(l =>
                    (l.IndexOf("ERR", StringComparison.OrdinalIgnoreCase) >= 0
                  || l.IndexOf("Exception", StringComparison.OrdinalIgnoreCase) >= 0
                  || l.IndexOf("Could not", StringComparison.OrdinalIgnoreCase) >= 0)
                  && !KnownNoise.Any(n => l.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(15)
                .ToList();

            // Last 10 lines before exit (skip the Unity memory stats noise)
            var tail = all
                .Reverse()
                .Where(l => !KnownNoise.Any(n => l.IndexOf(n, StringComparison.OrdinalIgnoreCase) >= 0))
                .Take(10)
                .Reverse()
                .ToList();

            var parts = new List<string>();
            if (realErrors.Count > 0)
                parts.Add($"[Errors]\n{string.Join("\n", realErrors)}");
            if (tail.Count > 0)
                parts.Add($"[Last output]\n{string.Join("\n", tail)}");
            if (parts.Count == 0)
                parts.Add("[No critical errors found — server may have exited cleanly]");

            parts.Add($"\nFull log: {path}");
            return string.Join("\n\n", parts);
        }
        catch { return null; }
    }

    public bool IsInstalled(string installDir) =>
        File.Exists(Path.Combine(installDir, ServerExeName));
}
