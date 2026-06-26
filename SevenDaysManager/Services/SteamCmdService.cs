using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace SevenDaysManager.Services;

public class SteamCmdService
{
    private const int AppId = 294420;
    private const string SteamCmdZipUrl = "https://steamcdn-a.akamaihd.net/client/installer/steamcmd.zip";
    private const string LatestBuildUrl = "https://api.steampowered.com/ISteamApps/UpToDateCheck/v1/?appid=294420&version=0&format=json";

    private static readonly HttpClient Http = new();

    private readonly string _steamCmdDir;
    public string SteamCmdExe { get; }
    public bool IsSteamCmdPresent => File.Exists(SteamCmdExe);

    public SteamCmdService()
    {
        var root     = App.DataStore.GetAppSettings().DefaultInstallRoot;
        _steamCmdDir = Path.Combine(root, "steamcmd");
        SteamCmdExe  = Path.Combine(_steamCmdDir, "steamcmd.exe");
    }

    // Download and extract SteamCMD if not already present
    public async Task EnsureSteamCmdAsync(IProgress<string>? progress = null, CancellationToken ct = default)
    {
        if (IsSteamCmdPresent)
        {
            progress?.Report("[SteamCMD] Already installed.");
            return;
        }

        Directory.CreateDirectory(_steamCmdDir);
        var zipPath = Path.Combine(_steamCmdDir, "steamcmd.zip");

        progress?.Report("[SteamCMD] Downloading steamcmd.zip...");
        var data = await Http.GetByteArrayAsync(SteamCmdZipUrl, ct);
        await File.WriteAllBytesAsync(zipPath, data, ct);

        progress?.Report("[SteamCMD] Extracting...");
        ZipFile.ExtractToDirectory(zipPath, _steamCmdDir, overwriteFiles: true);
        File.Delete(zipPath);

        progress?.Report("[SteamCMD] Ready.");
    }

    // Install or update the 7D2D dedicated server into installDir.
    // SteamCMD exits with code 7 the first time it self-updates — we retry once automatically.
    public async Task<int> InstallOrUpdateAsync(
        string installDir,
        IProgress<string>? progress = null,
        CancellationToken ct = default)
    {
        await EnsureDefenderExclusionsAsync(installDir, progress);
        await EnsureSteamCmdAsync(progress, ct);
        Directory.CreateDirectory(installDir);

        // A SteamCMD process from a previous run can hang and keep the "downloading" folder locked,
        // causing every new run to fail with state 0x202 (progress 0/0). Kill any strays first.
        KillOrphanedSteamCmd(progress);

        // If the game exe is missing but SteamCMD's "steamapps" folder is present, a previous
        // attempt left a partial/corrupt download state behind. SteamCMD can't reconcile this and
        // aborts with state 0x202/0x602 (progress 0/0). Wipe the whole steamapps folder so the
        // next run starts clean. (A real, fully-installed server still has the exe, so we leave it.)
        var serverExe     = Path.Combine(installDir, "7DaysToDieServer.exe");
        var steamAppsDir  = Path.Combine(installDir, "steamapps");
        if (!File.Exists(serverExe) && Directory.Exists(steamAppsDir))
        {
            progress?.Report("[SteamCMD] Clearing leftover partial-install state from a previous run…");
            try { Directory.Delete(steamAppsDir, recursive: true); }
            catch (Exception ex) { progress?.Report($"[SteamCMD] (could not fully clear: {ex.Message})"); }
        }

        int exitCode = -1;
        int attempt  = 0;

        do
        {
            if (attempt > 0)
            {
                progress?.Report("");
                progress?.Report($"[SteamCMD] Exit {exitCode} (self-update) — restarting install…");
                progress?.Report("");
            }

            exitCode = await RunSteamCmdAsync(installDir, progress, ct);
            attempt++;

        // Exit 7 = self-update needed; exit 8 = some SteamCMD versions use this after updating
        } while ((exitCode == 7 || exitCode == 8) && attempt < 3 && !ct.IsCancellationRequested);

        return exitCode;
    }

    private async Task<int> RunSteamCmdAsync(string installDir, IProgress<string>? progress, CancellationToken ct)
    {
        var psi = new ProcessStartInfo
        {
            FileName = SteamCmdExe,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        // SteamCMD's +force_install_dir chokes on paths containing spaces, even when quoted.
        // Pass the Windows 8.3 short path instead (falls back to the long path if unavailable).
        psi.ArgumentList.Add("+force_install_dir");
        psi.ArgumentList.Add(ToShortPath(installDir));
        psi.ArgumentList.Add("+login");
        psi.ArgumentList.Add("anonymous");
        psi.ArgumentList.Add("+app_update");
        psi.ArgumentList.Add(AppId.ToString());
        psi.ArgumentList.Add("validate");
        psi.ArgumentList.Add("+quit");

        psi.WorkingDirectory = _steamCmdDir;

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is { Length: > 0 } line)
                progress?.Report(line);
        };
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is { Length: > 0 } line)
                progress?.Report("[ERR] " + line);
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        try
        {
            await process.WaitForExitAsync(ct);
        }
        catch (OperationCanceledException)
        {
            // On cancel, make sure SteamCMD (and any child it spawned) is gone so it can't
            // linger and lock the download folder for the next attempt.
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch { }
            throw;
        }

        return process.ExitCode;
    }

    // Kill any steamcmd.exe instances running from our managed steamcmd directory.
    private void KillOrphanedSteamCmd(IProgress<string>? progress)
    {
        Process[] strays;
        try { strays = Process.GetProcessesByName("steamcmd"); }
        catch { return; }

        foreach (var p in strays)
        {
            try
            {
                // Only touch the steamcmd we manage, not some unrelated one elsewhere.
                if (!string.Equals(p.MainModule?.FileName, SteamCmdExe, StringComparison.OrdinalIgnoreCase))
                    continue;

                progress?.Report($"[SteamCMD] Stopping orphaned steamcmd (PID {p.Id}) from a previous run…");
                p.Kill(entireProcessTree: true);
                p.WaitForExit(5000);
            }
            catch { /* best-effort; may lack access to MainModule for some processes */ }
            finally { p.Dispose(); }
        }
    }

    // Add Windows Defender exclusions for the SteamCMD dir and install dir so real-time scanning
    // doesn't throttle the ~15 GB download to a crawl. Requires a UAC-elevated PowerShell process.
    public async Task EnsureDefenderExclusionsAsync(string installDir, IProgress<string>? progress = null)
    {
        progress?.Report("[Defender] Checking Windows Defender exclusions…");

        var pathsNeeded = new[] { _steamCmdDir, installDir }
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var existing = await GetDefenderExclusionsAsync();
        var missing  = pathsNeeded
            .Where(p => !existing.Any(e => e.Equals(p, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        if (missing.Count == 0)
        {
            progress?.Report("[Defender] Exclusions already in place — skipping.");
            return;
        }

        progress?.Report("[Defender] Adding exclusions — a UAC prompt may appear…");

        // Write to a temp .ps1 file so we don't have to fight shell quoting with UseShellExecute=true
        var scriptPath = Path.Combine(Path.GetTempPath(), $"7d2d_defender_{Guid.NewGuid():N}.ps1");
        var lines = missing.Select(p => $"Add-MpPreference -ExclusionPath '{p.Replace("'", "''")}'");
        await File.WriteAllLinesAsync(scriptPath, lines);

        var psi = new ProcessStartInfo
        {
            FileName        = "powershell.exe",
            Arguments       = $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            Verb            = "runas",
            UseShellExecute = true,
            WindowStyle     = ProcessWindowStyle.Hidden
        };

        try
        {
            using var proc = Process.Start(psi)!;
            await proc.WaitForExitAsync();
            progress?.Report(proc.ExitCode == 0
                ? "[Defender] Exclusions added — download speed should be much faster."
                : $"[Defender] Exclusion script exited {proc.ExitCode} — download may be slower than expected.");
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // 1223 = ERROR_CANCELLED — user denied the UAC prompt
            progress?.Report("[Defender] UAC prompt declined — continuing without exclusions (download may be slow).");
        }
        catch (Exception ex)
        {
            progress?.Report($"[Defender] Could not add exclusions ({ex.Message}) — continuing anyway.");
        }
        finally
        {
            try { File.Delete(scriptPath); } catch { }
        }
    }

    private static async Task<List<string>> GetDefenderExclusionsAsync()
    {
        var psi = new ProcessStartInfo
        {
            FileName               = "powershell.exe",
            Arguments              = "-NoProfile -NonInteractive -Command \"(Get-MpPreference).ExclusionPath\"",
            RedirectStandardOutput = true,
            UseShellExecute        = false,
            CreateNoWindow         = true
        };

        try
        {
            using var proc = Process.Start(psi)!;
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();
            return output.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                         .Select(l => l.Trim())
                         .Where(l => l.Length > 0)
                         .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetShortPathName(string lpszLongPath, StringBuilder lpszShortPath, uint cchBuffer);

    // Returns the 8.3 short path for a directory, or the original path if 8.3 names are unavailable.
    private static string ToShortPath(string longPath)
    {
        if (!Directory.Exists(longPath)) return longPath;

        var buffer = new StringBuilder(260);
        var result = GetShortPathName(longPath, buffer, (uint)buffer.Capacity);
        if (result == 0 || result > buffer.Capacity) return longPath;

        var shortPath = buffer.ToString();
        // If 8.3 generation is disabled on the volume, the path comes back unchanged (still has spaces).
        return string.IsNullOrWhiteSpace(shortPath) ? longPath : shortPath;
    }

    public record InstallInfo(string BuildId, bool IsUpToDate);

    // Read locally-installed build ID from the appmanifest SteamCMD writes after install/update
    public InstallInfo? GetInstallInfo(string installDir)
    {
        var manifest = Path.Combine(installDir, "steamapps", $"appmanifest_{AppId}.acf");
        if (!File.Exists(manifest)) return null;

        var content = File.ReadAllText(manifest);
        var build   = Regex.Match(content, @"""buildid""\s+""(\d+)""");
        if (!build.Success) return null;

        return new InstallInfo(build.Groups[1].Value, IsUpToDate: false); // IsUpToDate resolved by async check
    }

    // Fetch the latest published build ID for the 7D2D dedicated server from Steam
    public async Task<string?> GetLatestBuildIdAsync()
    {
        try
        {
            var json  = await Http.GetStringAsync(LatestBuildUrl);
            var match = Regex.Match(json, @"""required_version""\s*:\s*(\d+)");
            return match.Success ? match.Groups[1].Value : null;
        }
        catch { return null; }
    }

    // Kept for backwards compat — returns just the build ID string
    public string? GetInstalledBuild(string installDir) => GetInstallInfo(installDir)?.BuildId;
}
