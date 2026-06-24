namespace SevenDaysManager.Models;

public class Server
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "";
    public string Status { get; set; } = ServerStatus.Stopped;

    // File system
    public string InstallDir { get; set; } = "";

    // Networking
    public int ServerPort { get; set; } = 26900;
    public int TelnetPort { get; set; } = 8081;
    public string TelnetPassword { get; set; } = "";
    public int WebDashboardPort { get; set; } = 8080;
    public string ServerIp { get; set; } = "";

    // Identity / visibility
    public string ServerPassword { get; set; } = "";
    public string Description { get; set; } = "";
    public string WebsiteUrl { get; set; } = "";
    public int Visibility { get; set; } = 2;          // 0 = private, 1 = friends, 2 = public
    public int MaxPlayers { get; set; } = 8;

    // World
    public string GameWorld { get; set; } = "Navezgane";
    public string GameName { get; set; } = "My Game";
    public string WorldGenSeed { get; set; } = "";
    public int WorldGenSize { get; set; } = 6144;
    public bool EacEnabled { get; set; } = true;

    // Appearance
    public string LogoPath { get; set; } = "";

    // Extra serverconfig.xml overrides not promoted above
    public List<ConfigProperty> ExtraConfig { get; set; } = new();

    // Process tracking
    public int? LastPid { get; set; }
    public int? ExitCode { get; set; }
    public string? ServerLogPath { get; set; }
    public DateTime? ServerStartTime { get; set; }

    // Backup settings
    public string SaveDir { get; set; } = "";   // empty = auto-detect

    // Schedule
    public ScheduleConfig Schedule { get; set; } = new();

    // Discord
    public DiscordConfig Discord { get; set; } = new();

    // History
    public DateTime? LastWipedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsRwg => GameWorld.Equals("RWG", StringComparison.OrdinalIgnoreCase);
}

public record ConfigProperty(string Name, string Value);

public static class ServerStatus
{
    public const string Stopped  = "stopped";
    public const string Starting = "starting";
    public const string Running  = "running";
    public const string Stopping = "stopping";
}
