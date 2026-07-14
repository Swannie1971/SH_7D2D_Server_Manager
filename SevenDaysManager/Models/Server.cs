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

    // Behaviour
    public bool AutoStart { get; set; } = false;

    // ── Gameplay: the V3.0 sandbox code ───────────────────────────────────────
    //
    // V3.0 ("Dead Hot Summer") removed the individual gameplay properties from
    // serverconfig.xml — GameDifficulty, XPMultiplier, ZombieMove, BloodMoonFrequency,
    // LootAbundance and ~24 others. The game reads this one string instead. Writing the
    // old properties has no effect whatsoever; the server silently runs the sandbox code.
    //
    // Empty = the game's default (Adventurer). See SandboxCodeService.
    public string SandboxCode { get; set; } = "";

    // ── Server settings that are STILL individual properties in V3.0 ──────────
    // These were never part of the sandbox system and continue to work as normal XML.
    public int PlayerKillingMode               { get; set; } = 0;   // No killing
    public int MaxSpawnedZombies               { get; set; } = 64;
    public int MaxSpawnedAnimals               { get; set; } = 50;
    public int ServerMaxAllowedViewDistance    { get; set; } = 12;  // chunks
    public int LandClaimSize                   { get; set; } = 41;  // blocks radius
    public int LandClaimExpiryTime             { get; set; } = 7;   // days
    public int LandClaimOfflineDurabilityModifier { get; set; } = 0; // 0 = indestructible
    public int PlayerSafeZoneLevel             { get; set; } = 5;
    public int PlayerSafeZoneHours             { get; set; } = 5;

    // ── Dead in V3.0 ──────────────────────────────────────────────────────────
    // Retained ONLY so existing LiteDB documents still deserialize. The game ignores
    // these; they are no longer written to serverconfig.xml. Do not use them — set the
    // corresponding sandbox option in SandboxCode instead.
    [Obsolete("V3.0: folded into SandboxCode. Not written to serverconfig.xml.")]
    public int GameDifficulty      { get; set; } = 2;
    [Obsolete("V3.0: folded into SandboxCode.")] public int XPMultiplier        { get; set; } = 100;
    [Obsolete("V3.0: folded into SandboxCode.")] public int DayNightLength      { get; set; } = 60;
    [Obsolete("V3.0: folded into SandboxCode.")] public int DayLightLength      { get; set; } = 18;
    [Obsolete("V3.0: folded into SandboxCode.")] public int DropOnDeath         { get; set; } = 1;
    [Obsolete("V3.0: folded into SandboxCode.")] public int DropOnQuit          { get; set; } = 0;
    [Obsolete("V3.0: folded into SandboxCode.")] public int BloodMoonFrequency  { get; set; } = 7;
    [Obsolete("V3.0: folded into SandboxCode.")] public int BloodMoonEnemyCount { get; set; } = 8;
    [Obsolete("V3.0: folded into SandboxCode.")] public int ZombieMove          { get; set; } = 0;
    [Obsolete("V3.0: folded into SandboxCode.")] public int ZombieMoveNight     { get; set; } = 3;
    [Obsolete("V3.0: folded into SandboxCode.")] public int ZombieFeralMove     { get; set; } = 3;
    [Obsolete("V3.0: folded into SandboxCode.")] public int ZombieBMMove        { get; set; } = 3;
    [Obsolete("V3.0: folded into SandboxCode.")] public int LootAbundance       { get; set; } = 100;
    [Obsolete("V3.0: folded into SandboxCode.")] public int LootRespawnDays     { get; set; } = 7;
    [Obsolete("V3.0: folded into SandboxCode.")] public int AirDropFrequency    { get; set; } = 72;
    [Obsolete("V3.0: folded into SandboxCode.")] public int BloodMoonRange      { get; set; } = 0;

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
