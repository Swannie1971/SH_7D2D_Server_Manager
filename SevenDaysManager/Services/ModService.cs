using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public class ModService
{
    private static string ModsDir(string installDir) =>
        Path.Combine(installDir, "Mods");

    // ── Scan ─────────────────────────────────────────────────────────────────

    public Task<List<ModInfo>> GetModsAsync(string installDir) =>
        Task.Run(() => GetMods(installDir));

    private List<ModInfo> GetMods(string installDir)
    {
        var modsDir = ModsDir(installDir);
        if (!Directory.Exists(modsDir)) return new List<ModInfo>();

        var mods = new List<ModInfo>();

        foreach (var dir in Directory.GetDirectories(modsDir))
        {
            var folderName = Path.GetFileName(dir);
            var isEnabled  = !folderName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            var xmlPath    = Path.Combine(dir, "ModInfo.xml");

            var baseName = isEnabled ? folderName : folderName[..^".disabled".Length];
            var mod = new ModInfo
            {
                FolderPath  = dir,
                FolderName  = folderName,
                IsEnabled   = isEnabled,
                HasModInfo  = File.Exists(xmlPath),
                Name        = baseName,
                IsSystem    = baseName.StartsWith("TFP_", StringComparison.OrdinalIgnoreCase)
                           || baseName.StartsWith("0_TFP_", StringComparison.OrdinalIgnoreCase),
            };

            if (mod.HasModInfo)
            {
                try
                {
                    var xml = XDocument.Load(xmlPath);
                    mod.Name        = GetAttr(xml, "Name")        ?? mod.Name;
                    mod.Version     = GetAttr(xml, "Version")     ?? "";
                    mod.Author      = GetAttr(xml, "Author")      ?? "";
                    mod.Description = GetAttr(xml, "Description") ?? "";
                    mod.Website     = GetAttr(xml, "Website")     ?? "";
                }
                catch { /* malformed ModInfo.xml — use folder name */ }
            }

            mods.Add(mod);
        }

        return mods.OrderBy(m => m.Name).ToList();
    }

    private static string? GetAttr(XDocument xml, string elementName) =>
        xml.Descendants(elementName).FirstOrDefault()
           ?.Attribute("value")?.Value
           .Trim()
           .NullIfEmpty();

    // ── Enable / Disable ─────────────────────────────────────────────────────

    public ModInfo SetEnabled(ModInfo mod, bool enabled)
    {
        if (mod.IsSystem) throw new InvalidOperationException("System mods cannot be disabled.");
        if (mod.IsEnabled == enabled) return mod;

        var parent  = Path.GetDirectoryName(mod.FolderPath)!;
        var newName = enabled
            ? mod.FolderName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? mod.FolderName[..^".disabled".Length]
                : mod.FolderName
            : mod.FolderName + ".disabled";

        var newPath = Path.Combine(parent, newName);
        Directory.Move(mod.FolderPath, newPath);

        mod.FolderPath = newPath;
        mod.FolderName = newName;
        mod.IsEnabled  = enabled;
        return mod;
    }

    // ── Install from zip ─────────────────────────────────────────────────────

    public async Task<(bool ok, string error)> InstallFromZipAsync(string installDir, string zipPath)
    {
        return await Task.Run(() =>
        {
            try
            {
                var modsDir = ModsDir(installDir);
                Directory.CreateDirectory(modsDir);

                using var archive = ZipFile.OpenRead(zipPath);

                // Detect whether the zip has a single root folder or files at root
                var topDirs = archive.Entries
                    .Select(e => e.FullName.Split('/')[0])
                    .Where(s => !string.IsNullOrEmpty(s))
                    .Distinct()
                    .ToList();

                // Check if it looks like a single-folder mod
                bool hasSingleRoot = topDirs.Count == 1 &&
                    archive.Entries.All(e => e.FullName.StartsWith(topDirs[0] + "/") || e.FullName == topDirs[0]);

                string destFolder = hasSingleRoot
                    ? Path.Combine(modsDir, topDirs[0])
                    : Path.Combine(modsDir, Path.GetFileNameWithoutExtension(zipPath));

                if (Directory.Exists(destFolder))
                    Directory.Delete(destFolder, recursive: true);

                Directory.CreateDirectory(destFolder);

                foreach (var entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name)) continue; // directory entry

                    var relativePath = hasSingleRoot
                        ? entry.FullName[(topDirs[0].Length + 1)..]  // strip root folder
                        : entry.FullName;

                    if (string.IsNullOrEmpty(relativePath)) continue;

                    var destPath = Path.Combine(destFolder, relativePath.Replace('/', Path.DirectorySeparatorChar));

                    // Zip slip guard: reject entries that would escape the destination folder
                    var fullDest = Path.GetFullPath(destPath);
                    var fullBase = Path.GetFullPath(destFolder) + Path.DirectorySeparatorChar;
                    if (!fullDest.StartsWith(fullBase, StringComparison.OrdinalIgnoreCase))
                        continue;

                    Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
                    entry.ExtractToFile(destPath, overwrite: true);
                }

                return (true, "");
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        });
    }

    // ── Delete ───────────────────────────────────────────────────────────────

    public Task DeleteModAsync(ModInfo mod)
    {
        if (mod.IsSystem) throw new InvalidOperationException("System mods cannot be deleted.");
        return Task.Run(() => Directory.Delete(mod.FolderPath, recursive: true));
    }

    // ── Ensure Mods folder exists ─────────────────────────────────────────────

    public void EnsureModsFolder(string installDir) =>
        Directory.CreateDirectory(ModsDir(installDir));

    public string GetModsPath(string installDir) => ModsDir(installDir);
}

file static class StringExtensions
{
    public static string? NullIfEmpty(this string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s;
}
