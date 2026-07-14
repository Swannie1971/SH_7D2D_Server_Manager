using System.IO;
using System.Xml.Linq;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public class ServerConfigService
{
    // Writes serverconfig.xml to the server's install directory.
    // Always forces Telnet on with the stored password so we can connect.
    public void WriteConfig(Server server)
    {
        var path = ConfigPath(server.InstallDir);
        Directory.CreateDirectory(server.InstallDir);

        // Start from existing file so we preserve any keys we don't know about
        var doc = File.Exists(path)
            ? XDocument.Load(path)
            : new XDocument(new XDeclaration("1.0", "utf-8", null),
                            new XElement("ServerSettings"));

        var root = doc.Root ?? new XElement("ServerSettings");
        if (doc.Root is null) doc.Add(root);

        // Apply all promoted properties
        Set(root, "ServerName",            server.Name);
        Set(root, "ServerDescription",     server.Description);
        Set(root, "ServerWebsiteURL",      server.WebsiteUrl);
        Set(root, "ServerPassword",        server.ServerPassword);
        Set(root, "ServerPort",            server.ServerPort);
        Set(root, "ServerVisibility",      server.Visibility);
        Set(root, "ServerMaxPlayerCount",  server.MaxPlayers);
        Set(root, "GameWorld",             server.GameWorld);
        Set(root, "GameName",              server.GameName);
        Set(root, "WorldGenSeed",          server.WorldGenSeed);
        Set(root, "WorldGenSize",          server.WorldGenSize);
        Set(root, "EACEnabled",            server.EacEnabled);

        // Web dashboard
        Set(root, "WebDashboardEnabled",   server.WebDashboardPort > 0);
        Set(root, "WebDashboardPort",      server.WebDashboardPort);

        // Telnet — always enabled so the app can connect
        Set(root, "TelnetEnabled",         true);
        Set(root, "TelnetPort",            server.TelnetPort);
        Set(root, "TelnetPassword",        server.TelnetPassword);

        // ── Gameplay (V3.0) ──────────────────────────────────────────────────
        // One string carries every gameplay setting now. Empty = leave whatever is in the
        // file (the game ships a default), rather than writing an empty value the game
        // would then have to interpret.
        if (!string.IsNullOrWhiteSpace(server.SandboxCode))
            Set(root, "SandboxCode", server.SandboxCode.Trim());

        // Purge the pre-V3.0 gameplay properties. The game ignores them, but an admin
        // reading the file would reasonably assume "GameDifficulty=0" meant something —
        // and earlier versions of THIS app wrote them, so real configs are full of them.
        RemoveDeadV3Properties(root);

        // ── Settings that are still individual properties in V3.0 ────────────
        Set(root, "PlayerKillingMode",   server.PlayerKillingMode);
        Set(root, "MaxSpawnedZombies",   server.MaxSpawnedZombies);
        Set(root, "MaxSpawnedAnimals",   server.MaxSpawnedAnimals);
        Set(root, "ServerMaxAllowedViewDistance", server.ServerMaxAllowedViewDistance);
        Set(root, "LandClaimSize",       server.LandClaimSize);
        Set(root, "LandClaimExpiryTime", server.LandClaimExpiryTime);
        Set(root, "LandClaimOfflineDurabilityModifier", server.LandClaimOfflineDurabilityModifier);
        Set(root, "PlayerSafeZoneLevel", server.PlayerSafeZoneLevel);
        Set(root, "PlayerSafeZoneHours", server.PlayerSafeZoneHours);

        // Extra overrides from the Config tab
        foreach (var kv in server.ExtraConfig ?? Enumerable.Empty<ConfigProperty>())
            Set(root, kv.Name, kv.Value);

        doc.Save(path);
    }

    // Reads an existing serverconfig.xml and returns a partial Server populated from it.
    // Caller merges the returned values into their own Server object as needed.
    public Server ReadConfig(string installDir)
    {
        var path = ConfigPath(installDir);
        if (!File.Exists(path))
            return new Server { InstallDir = installDir };

        var doc  = XDocument.Load(path);
        var root = doc.Root!;

        return new Server
        {
            InstallDir       = installDir,
            Name             = Get(root, "ServerName"),
            Description      = Get(root, "ServerDescription"),
            WebsiteUrl       = Get(root, "ServerWebsiteURL"),
            ServerPassword   = Get(root, "ServerPassword"),
            ServerPort       = GetInt(root, "ServerPort",           26900),
            Visibility       = GetInt(root, "ServerVisibility",     2),
            MaxPlayers       = GetInt(root, "ServerMaxPlayerCount", 8),
            GameWorld        = Get(root,  "GameWorld",              "Navezgane"),
            GameName         = Get(root,  "GameName",               "My Game"),
            WorldGenSeed     = Get(root,  "WorldGenSeed"),
            WorldGenSize     = GetInt(root, "WorldGenSize",         6144),
            EacEnabled       = GetBool(root, "EACEnabled",          true),
            TelnetPort       = GetInt(root, "TelnetPort",           8081),
            TelnetPassword   = Get(root,  "TelnetPassword"),
            WebDashboardPort = GetInt(root, "WebDashboardPort",     8080),

            // V3.0: every gameplay setting lives in this one string.
            SandboxCode      = Get(root, "SandboxCode"),

            PlayerKillingMode   = GetInt(root, "PlayerKillingMode",   0),
            MaxSpawnedZombies   = GetInt(root, "MaxSpawnedZombies",   64),
            MaxSpawnedAnimals   = GetInt(root, "MaxSpawnedAnimals",   50),
            ServerMaxAllowedViewDistance = GetInt(root, "ServerMaxAllowedViewDistance", 12),
            LandClaimSize       = GetInt(root, "LandClaimSize",       41),
            LandClaimExpiryTime = GetInt(root, "LandClaimExpiryTime", 7),
            LandClaimOfflineDurabilityModifier = GetInt(root, "LandClaimOfflineDurabilityModifier", 0),
            PlayerSafeZoneLevel = GetInt(root, "PlayerSafeZoneLevel", 5),
            PlayerSafeZoneHours = GetInt(root, "PlayerSafeZoneHours", 5),
        };
    }

    public bool ConfigExists(string installDir) => File.Exists(ConfigPath(installDir));

    public static string ConfigPath(string installDir) =>
        Path.Combine(installDir, "serverconfig.xml");

    /// <summary>
    /// Gameplay properties that V3.0 folded into <c>SandboxCode</c>. The game no longer reads
    /// any of them. We strip them on save so the file doesn't lie about what's in effect —
    /// earlier builds of this app wrote them, so most existing configs contain a stale block.
    /// </summary>
    private static readonly string[] DeadInV3 =
    {
        "GameDifficulty", "XPMultiplier", "DayNightLength", "DayLightLength",
        "DropOnDeath", "DropOnQuit", "BloodMoonFrequency", "BloodMoonRange",
        "BloodMoonEnemyCount", "BloodMoonWarning", "ZombieMove", "ZombieMoveNight",
        "ZombieFeralMove", "ZombieBMMove", "ZombieFeralSense", "AISmellMode",
        "LootAbundance", "LootRespawnDays", "AirDropFrequency", "AirDropMarker",
        "BlockDamagePlayer", "BlockDamageAI", "BlockDamageAIBM", "BiomeProgression",
        "StormFreq", "DeathPenalty", "JarRefund", "EnemySpawnMode", "EnemyDifficulty",
        "QuestProgressionDailyLimit",
    };

    private static void RemoveDeadV3Properties(XElement root)
    {
        root.Elements("property")
            .Where(e => DeadInV3.Contains(e.Attribute("name")?.Value))
            .ToList()                       // materialise before removing
            .ForEach(e => e.Remove());
    }

    // ── XML helpers ───────────────────────────────────────────────────────────

    private static void Set(XElement root, string name, object value)
    {
        var el = root.Elements("property")
                     .FirstOrDefault(e => e.Attribute("name")?.Value == name);
        if (el is null)
        {
            el = new XElement("property", new XAttribute("name", name));
            root.Add(el);
        }
        el.SetAttributeValue("value", value is bool b ? b.ToString().ToLower() : value?.ToString() ?? "");
    }

    private static string Get(XElement root, string name, string fallback = "")
    {
        return root.Elements("property")
                   .FirstOrDefault(e => e.Attribute("name")?.Value == name)
                   ?.Attribute("value")?.Value ?? fallback;
    }

    private static int GetInt(XElement root, string name, int fallback = 0) =>
        int.TryParse(Get(root, name), out var v) ? v : fallback;

    private static bool GetBool(XElement root, string name, bool fallback = false) =>
        Get(root, name, fallback.ToString().ToLower()) is "true" or "1";
}
