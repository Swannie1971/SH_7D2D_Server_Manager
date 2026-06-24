using System.IO;
using System.IO.Compression;
using System.Text.RegularExpressions;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public class BackupService
{
    // ── Log parsing ───────────────────────────────────────────────────────────

    private static readonly Regex UserDataLogRx = new(
        @"UserDataFolder[:\s]+(.+?)[\r\n]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex SaveGameLogRx = new(
        @"SaveGameFolder[:\s]+(.+?)[\r\n]", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static string ReadLogHead(string logPath, int maxLines = 400)
    {
        using var fs = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        var sb = new System.Text.StringBuilder();
        int i = 0;
        while (i++ < maxLines && sr.ReadLine() is { } line)
            sb.AppendLine(line);
        return sb.ToString();
    }

    // ── Resolution ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the UserDataFolder (e.g. %APPDATA%\7DaysToDie) by reading the
    /// server log, ExtraConfig, or falling back to AppData default.
    /// </summary>
    public static string GetUserDataFolder(Server server)
    {
        // 1. ExtraConfig override
        var udf = server.ExtraConfig.FirstOrDefault(p => p.Name == "UserDataFolder")?.Value ?? "";
        if (!string.IsNullOrWhiteSpace(udf))
            return Path.IsPathRooted(udf) ? udf : Path.Combine(server.InstallDir, udf);

        // 2. Parse server log
        if (!string.IsNullOrEmpty(server.ServerLogPath) && File.Exists(server.ServerLogPath))
        {
            try
            {
                var text = ReadLogHead(server.ServerLogPath);
                var m = UserDataLogRx.Match(text);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            catch { }
        }

        // 3. Default
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "7DaysToDie");
    }

    /// <summary>
    /// Resolves the actual save folder. For RWG worlds the folder name is the
    /// generated world name (e.g. WestXuyofuCounty), NOT "RWG".
    /// Scans the Saves directory for a subfolder matching GameName.
    /// </summary>
    public static (string saveFolder, string worldName) FindSaveFolder(Server server)
    {
        // 1. Manual override
        if (!string.IsNullOrWhiteSpace(server.SaveDir))
        {
            var worldName = Path.GetFileName(Path.GetDirectoryName(server.SaveDir)) ?? "";
            return (server.SaveDir, worldName);
        }

        // 2. Parse SaveGameFolder from log — gives Saves\ root, then scan for GameName
        if (!string.IsNullOrEmpty(server.ServerLogPath) && File.Exists(server.ServerLogPath))
        {
            try
            {
                var text = ReadLogHead(server.ServerLogPath);
                var m = SaveGameLogRx.Match(text);
                if (m.Success)
                {
                    var savesRoot = m.Groups[1].Value.Trim();
                    var found     = ScanSavesRoot(savesRoot, server.GameName);
                    if (found.saveFolder is not null) return found;
                }

                // Also try UserDataFolder\Saves
                m = UserDataLogRx.Match(text);
                if (m.Success)
                {
                    var savesRoot = Path.Combine(m.Groups[1].Value.Trim(), "Saves");
                    var found     = ScanSavesRoot(savesRoot, server.GameName);
                    if (found.saveFolder is not null) return found;
                }
            }
            catch { }
        }

        // 3. Scan AppData\7DaysToDie\Saves for a subfolder matching GameName
        {
            var savesRoot = Path.Combine(GetUserDataFolder(server), "Saves");
            var found     = ScanSavesRoot(savesRoot, server.GameName);
            if (found.saveFolder is not null) return found;
        }

        // 4. Fallback — best guess path (may not exist)
        var udf = GetUserDataFolder(server);
        return (Path.Combine(udf, "Saves", server.GameWorld, server.GameName), server.GameWorld);
    }

    /// <summary>
    /// Scans all world subdirectories under savesRoot looking for one that
    /// contains a subfolder named gameName.
    /// </summary>
    private static (string? saveFolder, string worldName) ScanSavesRoot(string savesRoot, string gameName)
    {
        if (!Directory.Exists(savesRoot)) return (null, "");
        foreach (var worldDir in Directory.GetDirectories(savesRoot))
        {
            var candidate = Path.Combine(worldDir, gameName);
            if (Directory.Exists(candidate))
                return (candidate, Path.GetFileName(worldDir));
        }
        return (null, "");
    }

    /// <summary>
    /// Returns the GeneratedWorlds folder for the given world name.
    /// </summary>
    public static string? FindGeneratedWorldFolder(Server server, string worldName)
    {
        if (string.IsNullOrWhiteSpace(worldName) ||
            worldName.Equals("RWG", StringComparison.OrdinalIgnoreCase) ||
            worldName.Equals("Navezgane", StringComparison.OrdinalIgnoreCase))
            return null;

        var path = Path.Combine(GetUserDataFolder(server), "GeneratedWorlds", worldName);
        return Directory.Exists(path) ? path : null;
    }

    public static string GetBackupFolder(Server server) =>
        Path.Combine(server.InstallDir, "Backups");

    // ── List ──────────────────────────────────────────────────────────────────

    public List<BackupInfo> ListBackups(Server server)
    {
        var dir = GetBackupFolder(server);
        if (!Directory.Exists(dir)) return new();

        return Directory.GetFiles(dir, "*.zip")
            .Select(f =>
            {
                var info = new FileInfo(f);
                return new BackupInfo
                {
                    FilePath  = f,
                    FileName  = info.Name,
                    CreatedAt = info.CreationTime,
                    SizeBytes = info.Length
                };
            })
            .OrderByDescending(b => b.CreatedAt)
            .ToList();
    }

    // ── Create ────────────────────────────────────────────────────────────────

    public async Task CreateBackupAsync(Server server, IProgress<string>? progress = null)
    {
        var (saveFolder, worldName) = FindSaveFolder(server);
        var generatedWorldFolder    = FindGeneratedWorldFolder(server, worldName);
        var backupFolder            = GetBackupFolder(server);
        var configFile              = Path.Combine(server.InstallDir, "serverconfig.xml");

        if (!Directory.Exists(saveFolder))
            throw new DirectoryNotFoundException(
                $"Save folder not found:\n{saveFolder}\n\n" +
                "Start the server at least once so the world generates, then try again.");

        Directory.CreateDirectory(backupFolder);

        var timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        var safeName  = string.Concat(server.GameName.Select(c =>
            Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var zipPath   = Path.Combine(backupFolder, $"{safeName}_{timestamp}.zip");

        progress?.Report("Scanning files…");

        await Task.Run(() =>
        {
            var saveFiles  = Directory.GetFiles(saveFolder, "*", SearchOption.AllDirectories);
            var worldFiles = generatedWorldFolder is not null
                ? Directory.GetFiles(generatedWorldFolder, "*", SearchOption.AllDirectories)
                : Array.Empty<string>();

            if (saveFiles.Length == 0)
                throw new InvalidOperationException(
                    $"Save folder exists but contains no files:\n{saveFolder}\n\n" +
                    "The world may not have generated yet. Join the server once, then try again.");

            int total = saveFiles.Length + worldFiles.Length + (File.Exists(configFile) ? 1 : 0);
            int done  = 0;

            using var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create);

            // Save data under Saves\<WorldName>\<GameName>\
            foreach (var file in saveFiles)
            {
                var entry = Path.Combine("Saves", worldName, server.GameName,
                    Path.GetRelativePath(saveFolder, file));
                progress?.Report($"[{done + 1}/{total}] {Path.GetFileName(file)}");
                zip.CreateEntryFromFile(file, entry, CompressionLevel.Optimal);
                done++;
            }

            // Generated world data under GeneratedWorlds\<WorldName>\
            foreach (var file in worldFiles)
            {
                var entry = Path.Combine("GeneratedWorlds", worldName,
                    Path.GetRelativePath(generatedWorldFolder!, file));
                progress?.Report($"[{done + 1}/{total}] {Path.GetFileName(file)}");
                zip.CreateEntryFromFile(file, entry, CompressionLevel.Optimal);
                done++;
            }

            // serverconfig.xml
            if (File.Exists(configFile))
            {
                progress?.Report($"[{done + 1}/{total}] serverconfig.xml");
                zip.CreateEntryFromFile(configFile, "serverconfig.xml", CompressionLevel.Optimal);
                done++;
            }
        });

        var size = new BackupInfo { SizeBytes = new FileInfo(zipPath).Length }.SizeLabel;
        progress?.Report($"Done — {size}");
    }

    // ── Restore ───────────────────────────────────────────────────────────────

    public async Task RestoreBackupAsync(Server server, BackupInfo backup, IProgress<string>? progress = null)
    {
        var udf      = GetUserDataFolder(server);
        var savesDir = Path.Combine(udf, "Saves");
        var genDir   = Path.Combine(udf, "GeneratedWorlds");
        var tempDir  = Path.Combine(backupTempPath(server), "restore_temp");

        progress?.Report("Extracting archive…");
        await Task.Run(() =>
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            ZipFile.ExtractToDirectory(backup.FilePath, tempDir);
        });

        progress?.Report("Restoring save data…");
        await Task.Run(() =>
        {
            // Restore Saves\*\*
            var tempSaves = Path.Combine(tempDir, "Saves");
            if (Directory.Exists(tempSaves))
                CopyDirectory(tempSaves, savesDir, overwrite: true);

            // Restore GeneratedWorlds\*
            var tempGen = Path.Combine(tempDir, "GeneratedWorlds");
            if (Directory.Exists(tempGen))
                CopyDirectory(tempGen, genDir, overwrite: true);
        });

        if (Directory.Exists(tempDir))
            Directory.Delete(tempDir, true);

        progress?.Report("Restore complete.");
    }

    private static string backupTempPath(Server server) =>
        Path.Combine(server.InstallDir, "Backups", ".tmp");

    private static void CopyDirectory(string src, string dst, bool overwrite)
    {
        Directory.CreateDirectory(dst);
        foreach (var file in Directory.GetFiles(src, "*", SearchOption.AllDirectories))
        {
            var rel  = Path.GetRelativePath(src, file);
            var dest = Path.Combine(dst, rel);
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            File.Copy(file, dest, overwrite);
        }
    }

    // ── Delete ────────────────────────────────────────────────────────────────

    public void DeleteBackup(BackupInfo backup)
    {
        if (File.Exists(backup.FilePath))
            File.Delete(backup.FilePath);
    }
}
