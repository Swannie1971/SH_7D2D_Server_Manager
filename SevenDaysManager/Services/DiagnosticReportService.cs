using System.Diagnostics;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

/// <summary>
/// Builds a one-shot diagnostic report for the "random lag spikes" investigation.
///
/// The report is aimed squarely at the CPU-contention hypothesis: it reads back the priority
/// and affinity ACTUALLY applied to the live server process (not just what's configured — a
/// setting that didn't take is the first thing to rule out), shows what else is competing for
/// the CPU, and pulls the save-event timestamps out of the log so a felt spike can be lined up
/// against a world save. It's plain text so it pastes straight into Discord.
///
/// Deliberately dependency-free: CPU via the registry, RAM via GlobalMemoryStatusEx, drive via
/// DriveInfo — the same approach OverviewViewModel already uses, so this adds no NuGet package.
///
/// Temporary: this exists to diagnose one issue on a tester's machine. Once we know the cause,
/// the report and its button come out. See the [DIAGNOSTIC] marker on the UI button.
/// </summary>
public static class DiagnosticReportService
{
    public static string Build(Server server)
    {
        var sb = new StringBuilder();
        sb.AppendLine("===== 7D2D MANAGER - DIAGNOSTIC REPORT =====");
        sb.AppendLine($"Generated : {DateTime.Now:yyyy-MM-dd HH:mm:ss} (local)");
        sb.AppendLine($"App       : v{UpdateService.CurrentVersion}");
        sb.AppendLine($"Server    : {server.Name}");
        sb.AppendLine();

        AppendSystemProfile(sb, server);
        AppendPerformanceSettings(sb, server);
        AppendLiveProcess(sb, server);
        AppendCompetingProcesses(sb);
        AppendSaveEvents(sb, server);
        AppendErrorTail(sb, server);

        sb.AppendLine();
        sb.AppendLine("===== END OF REPORT =====");
        return sb.ToString();
    }

    // ── System profile ────────────────────────────────────────────────────────
    // Confirms the hardware the spikes are happening on: core count (so we know if the
    // affinity numbers make sense on THIS box, which may differ from ours) and RAM.
    private static void AppendSystemProfile(StringBuilder sb, Server server)
    {
        sb.AppendLine("----- SYSTEM -----");
        sb.AppendLine($"OS            : {Environment.OSVersion.VersionString}");
        sb.AppendLine($"CPU           : {GetProcessorName()}");
        sb.AppendLine($"Logical cores : {Environment.ProcessorCount}");
        var ramGb = GetTotalRamGb();
        if (ramGb > 0) sb.AppendLine($"RAM           : {ramGb} GB");
        sb.AppendLine($"Server drive  : {DescribeDrive(server.InstallDir)}");
        sb.AppendLine();
    }

    private static string GetProcessorName()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(
                @"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            return key?.GetValue("ProcessorNameString")?.ToString()?.Trim() ?? "Unknown";
        }
        catch { return "Unknown"; }
    }

    private static int GetTotalRamGb()
    {
        try
        {
            var s = new MEMORYSTATUSEX { dwLength = 64 };
            return GlobalMemoryStatusEx(ref s) ? (int)(s.ullTotalPhys / 1_073_741_824UL) : 0;
        }
        catch { return 0; }
    }

    // Drive letter + free space. We don't try to detect SSD vs HDD here (that needs the
    // storage WMI namespace and a package we're avoiding); free space is the thing worth
    // knowing anyway — a nearly-full drive is its own source of stutter.
    private static string DescribeDrive(string? installDir)
    {
        if (string.IsNullOrWhiteSpace(installDir)) return "(install dir not set)";
        try
        {
            var root = Path.GetPathRoot(Path.GetFullPath(installDir));
            if (string.IsNullOrEmpty(root)) return installDir;
            var di = new DriveInfo(root);
            var freeGb  = di.AvailableFreeSpace / 1024.0 / 1024.0 / 1024.0;
            var totalGb = di.TotalSize          / 1024.0 / 1024.0 / 1024.0;
            return $"{di.Name.TrimEnd('\\')} - {freeGb:0.0} GB free of {totalGb:0.0} GB";
        }
        catch (Exception ex) { return $"(could not resolve: {ex.Message})"; }
    }

    // ── Performance settings (configured vs. actually applied) ─────────────────
    // THE key section. If the tester reports spikes but priority didn't actually take,
    // that's the finding — so read it back off the live process, not just the config.
    private static void AppendPerformanceSettings(StringBuilder sb, Server server)
    {
        sb.AppendLine("----- PERFORMANCE SETTINGS -----");
        sb.AppendLine($"Configured priority : {(string.IsNullOrWhiteSpace(server.ProcessPriority) ? "Normal (default)" : server.ProcessPriority)}");
        sb.AppendLine($"Configured affinity : {(server.CpuAffinityEnabled ? $"ON - {server.CpuAffinityCores} cores" : "OFF (all cores)")}");

        var proc = TryGetServerProcess(server);
        if (proc is null)
        {
            sb.AppendLine("Live process        : not running - start the server to confirm the settings applied.");
        }
        else
        {
            try
            {
                sb.AppendLine($"Applied priority    : {proc.PriorityClass}");
                var mask = (long)proc.ProcessorAffinity;
                var cores = BitOperations.PopCount((ulong)mask);
                sb.AppendLine($"Applied affinity    : 0x{mask:X} ({cores} of {Environment.ProcessorCount} cores)");
            }
            catch (Exception ex) { sb.AppendLine($"Live process        : (could not read: {ex.Message})"); }
        }
        sb.AppendLine();
    }

    // ── Live process stats ────────────────────────────────────────────────────
    private static void AppendLiveProcess(StringBuilder sb, Server server)
    {
        sb.AppendLine("----- SERVER PROCESS -----");
        var proc = TryGetServerProcess(server);
        if (proc is null)
        {
            sb.AppendLine("(server is not running)");
            sb.AppendLine();
            return;
        }

        try
        {
            proc.Refresh();
            var memMb = proc.WorkingSet64 / 1024.0 / 1024.0;
            var upFor = server.ServerStartTime is { } t ? DateTime.UtcNow - t : (TimeSpan?)null;
            sb.AppendLine($"PID           : {proc.Id}");
            sb.AppendLine($"Memory        : {memMb:0} MB");
            sb.AppendLine($"Threads       : {proc.Threads.Count}");
            if (upFor is { } up) sb.AppendLine($"Uptime        : {(int)up.TotalHours}h {up.Minutes}m");
        }
        catch (Exception ex) { sb.AppendLine($"(could not read process: {ex.Message})"); }
        sb.AppendLine();
    }

    // ── Competing processes ───────────────────────────────────────────────────
    // What else is holding memory right now — the game client, a browser, etc. This is the
    // contention we're trying to bias the server away from.
    private static void AppendCompetingProcesses(StringBuilder sb)
    {
        sb.AppendLine("----- TOP PROCESSES BY MEMORY (contention snapshot) -----");
        try
        {
            var top = Process.GetProcesses()
                .Select(p => { try { return (p.ProcessName, Mb: p.WorkingSet64 / 1024.0 / 1024.0); } catch { return (p.ProcessName, Mb: 0.0); } })
                .Where(x => x.Mb > 0)
                .OrderByDescending(x => x.Mb)
                .Take(12);
            foreach (var (name, mb) in top)
                sb.AppendLine($"  {mb,7:0} MB  {name}");
        }
        catch (Exception ex) { sb.AppendLine($"(unavailable: {ex.Message})"); }
        sb.AppendLine();
    }

    // ── Stall check (the actual verdict) ──────────────────────────────────────
    // We traced the lag spikes to our own metrics polling: sending the telnet "mem" command
    // forced a stop-the-world GC in the server (~350 ms on a Ryzen 7600, ~810 ms on a Xeon
    // E5-2690 v4). MetricsPoller no longer sends it, so a fixed build should show ZERO 'mem'
    // commands and no GC "Total: … MarkObjects" stalls. That's what this section reports.
    private static void AppendSaveEvents(StringBuilder sb, Server server)
    {
        sb.AppendLine("----- STALL CHECK (was the lag-spike fix effective?) -----");
        var path = LogPath(server);
        if (path is null) { sb.AppendLine("(no log file found)"); sb.AppendLine(); return; }

        try
        {
            var lines = ReadAllLinesShared(path).ToList();

            // 1. Did anything still issue the GC-forcing command?
            var memCalls = lines.Count(l => l.Contains("Executing command 'mem'", StringComparison.OrdinalIgnoreCase));

            // 2. The stall itself: "Total: 362.397200 ms (FindLiveObjects: … MarkObjects: …)"
            var stalls = lines
                .Where(l => l.TrimStart().StartsWith("Total:", StringComparison.OrdinalIgnoreCase)
                         && l.Contains("MarkObjects", StringComparison.OrdinalIgnoreCase))
                .ToList();

            sb.AppendLine($"'mem' commands issued : {memCalls}   (expected 0 after the fix)");
            sb.AppendLine($"GC stalls in log      : {stalls.Count}   (expected 0 after the fix)");

            if (stalls.Count > 0)
            {
                // Surface the worst one — that's the size of the freeze players actually felt.
                var worst = stalls
                    .Select(l => (Line: l, Ms: ParseLeadingMs(l)))
                    .OrderByDescending(x => x.Ms)
                    .First();
                sb.AppendLine($"Worst stall           : {worst.Ms:0} ms");
                sb.AppendLine();
                sb.AppendLine("  Recent stalls:");
                foreach (var s in stalls.AsEnumerable().Reverse().Take(8).Reverse())
                    sb.AppendLine($"    {s.Trim()}");
            }
            else
            {
                sb.AppendLine("No forced-GC stalls found — this is what a fixed build looks like.");
            }
        }
        catch (Exception ex) { sb.AppendLine($"(could not read log: {ex.Message})"); }
        sb.AppendLine();
    }

    // ── Error tail ────────────────────────────────────────────────────────────
    private static void AppendErrorTail(StringBuilder sb, Server server)
    {
        sb.AppendLine("----- LOG TAIL (last 25 lines) -----");
        var path = LogPath(server);
        if (path is null) { sb.AppendLine("(no log file found)"); return; }
        sb.AppendLine($"(full log: {path})");
        try
        {
            var tail = ReadAllLinesShared(path).Reverse().Take(25).Reverse();
            foreach (var l in tail) sb.AppendLine(l);
        }
        catch (Exception ex) { sb.AppendLine($"(could not read log: {ex.Message})"); }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Pull the millisecond figure out of a GC line: "Total: 362.397200 ms (FindLiveObjects: …".
    /// Returns 0 if the shape isn't what we expect — this is a report, not a parser we should
    /// throw from.
    /// </summary>
    private static double ParseLeadingMs(string line)
    {
        var m = System.Text.RegularExpressions.Regex.Match(line, @"Total:\s*([\d.]+)\s*ms");
        return m.Success && double.TryParse(m.Groups[1].Value,
                   System.Globalization.NumberStyles.Float,
                   System.Globalization.CultureInfo.InvariantCulture, out var ms)
               ? ms : 0;
    }

    /// <summary>The log path we launch the server with (install dir), if it exists.</summary>
    public static string? LogPath(Server server)
    {
        if (string.IsNullOrWhiteSpace(server.InstallDir)) return null;
        var p = Path.Combine(server.InstallDir, "manager_server.log");
        return File.Exists(p) ? p : null;
    }

    // The server holds the log file open for writing, so a plain File.ReadLines throws
    // "being used by another process". Open with ReadWrite share to read it live.
    private static IEnumerable<string> ReadAllLinesShared(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        var lines = new List<string>();
        string? line;
        while ((line = sr.ReadLine()) is not null) lines.Add(line);
        return lines;
    }

    private static Process? TryGetServerProcess(Server server)
    {
        if (server.LastPid is not { } pid) return null;
        try
        {
            var p = Process.GetProcessById(pid);
            return p.HasExited ? null : p;
        }
        catch { return null; }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MEMORYSTATUSEX
    {
        public uint  dwLength, dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile,
                     ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GlobalMemoryStatusEx(ref MEMORYSTATUSEX lpBuffer);
}
