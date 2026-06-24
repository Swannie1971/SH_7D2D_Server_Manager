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
        };
    }

    public bool ConfigExists(string installDir) => File.Exists(ConfigPath(installDir));

    public static string ConfigPath(string installDir) =>
        Path.Combine(installDir, "serverconfig.xml");

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
