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

    // The game loads a mod folder if — and only if — it contains a readable ModInfo.xml. It
    // does NOT care what the folder is called. So "disabling" by renaming the FOLDER to
    // *.disabled never worked: MyMod.disabled/ModInfo.xml still loads. We disable by renaming
    // the manifest itself to this, which the game then can't find, so it skips the folder.
    private const string ModInfoFile         = "ModInfo.xml";
    private const string DisabledModInfoFile = "ModInfo.xml.disabled";

    private List<ModInfo> GetMods(string installDir)
    {
        var modsDir = ModsDir(installDir);
        if (!Directory.Exists(modsDir)) return new List<ModInfo>();

        var mods = new List<ModInfo>();

        foreach (var dir in Directory.GetDirectories(modsDir))
        {
            // Migrate any left-over legacy *.disabled FOLDER to the new scheme first, so the
            // rest of the scan only ever sees clean folder names and manifest-based state — and
            // so a mod the user disabled the old (non-working) way actually stops loading.
            var normalisedDir = MigrateLegacyDisabledFolder(dir);

            var folderName = Path.GetFileName(normalisedDir);
            var xmlPath         = Path.Combine(normalisedDir, ModInfoFile);
            var disabledXmlPath = Path.Combine(normalisedDir, DisabledModInfoFile);

            // Enabled = the game can see a ModInfo.xml. Disabled = only our renamed marker is
            // present. A folder with neither still isn't a loadable mod, so treat it as disabled.
            var hasModInfo  = File.Exists(xmlPath);
            var hasDisabled = File.Exists(disabledXmlPath);
            var isEnabled   = hasModInfo;

            var mod = new ModInfo
            {
                FolderPath  = normalisedDir,
                FolderName  = folderName,
                IsEnabled   = isEnabled,
                HasModInfo  = hasModInfo || hasDisabled,
                Name        = folderName,
                IsSystem    = folderName.StartsWith("TFP_", StringComparison.OrdinalIgnoreCase)
                           || folderName.StartsWith("0_TFP_", StringComparison.OrdinalIgnoreCase),
            };

            // Read metadata from whichever manifest exists (the disabled one is just renamed).
            var manifest = hasModInfo ? xmlPath : hasDisabled ? disabledXmlPath : null;
            if (manifest is not null)
            {
                try
                {
                    var xml = XDocument.Load(manifest);
                    mod.Name        = GetAttr(xml, "Name")        ?? mod.Name;
                    mod.Version     = GetAttr(xml, "Version")     ?? "";
                    mod.Author      = GetAttr(xml, "Author")      ?? "";
                    mod.Description = GetAttr(xml, "Description") ?? "";
                    mod.Website     = GetAttr(xml, "Website")     ?? "";
                }
                catch { /* malformed manifest — fall back to the folder name */ }
            }

            mods.Add(mod);
        }

        return mods.OrderBy(m => m.Name).ToList();
    }

    /// <summary>
    /// Convert a legacy "*.disabled" folder (the old, non-working disable scheme) into the new
    /// scheme: strip the suffix off the folder and rename its ModInfo.xml to the disabled marker
    /// so the mod actually stays off. Returns the (possibly new) folder path. A no-op for folders
    /// that aren't legacy-disabled.
    /// </summary>
    private static string MigrateLegacyDisabledFolder(string dir)
    {
        var name = Path.GetFileName(dir);
        if (!name.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)) return dir;

        var parent   = Path.GetDirectoryName(dir)!;
        var cleanName = name[..^".disabled".Length];
        var cleanDir  = Path.Combine(parent, cleanName);

        try
        {
            // If a clean-named folder already exists we can't safely merge — leave the legacy
            // folder as-is rather than risk clobbering. Its manifest rename below still disables it.
            if (!Directory.Exists(cleanDir))
            {
                Directory.Move(dir, cleanDir);
            }
            else
            {
                cleanDir = dir; // couldn't rename the folder; still fix the manifest in place
            }

            // Now make sure the mod is genuinely disabled: hide its ModInfo.xml.
            var xml         = Path.Combine(cleanDir, ModInfoFile);
            var disabledXml = Path.Combine(cleanDir, DisabledModInfoFile);
            if (File.Exists(xml) && !File.Exists(disabledXml))
                File.Move(xml, disabledXml);

            return cleanDir;
        }
        catch
        {
            // Any IO failure: leave whatever we managed, and report the original folder so the
            // scan doesn't crash. Worst case the user re-toggles it.
            return Directory.Exists(cleanDir) ? cleanDir : dir;
        }
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

        // Disable/enable by renaming the manifest, not the folder — the game keys off ModInfo.xml
        // and ignores the folder name entirely, so a folder rename never actually stopped a load.
        var xml         = Path.Combine(mod.FolderPath, ModInfoFile);
        var disabledXml = Path.Combine(mod.FolderPath, DisabledModInfoFile);

        if (enabled)
        {
            if (File.Exists(disabledXml) && !File.Exists(xml))
                File.Move(disabledXml, xml);
            else if (!File.Exists(xml))
                // Nothing to restore — the folder has no manifest at all (a malformed or
                // data-only mod). We can't make the game load it by renaming something that
                // isn't there, so don't pretend we did.
                throw new InvalidOperationException(
                    "This mod has no ModInfo.xml, so it can't be enabled through the manager.");
        }
        else
        {
            if (File.Exists(xml))
            {
                // If a stale marker is somehow already there, drop it so the Move can't fail.
                if (File.Exists(disabledXml)) File.Delete(disabledXml);
                File.Move(xml, disabledXml);
            }
            else if (!File.Exists(disabledXml))
                throw new InvalidOperationException(
                    "This mod has no ModInfo.xml, so there's nothing to disable.");
        }

        mod.IsEnabled = enabled;
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
