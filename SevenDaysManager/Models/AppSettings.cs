namespace SevenDaysManager.Models;

public class AppSettings
{
    public string Id { get; set; } = "singleton";

    // Paths
    public string DefaultInstallRoot { get; set; } = @"C:\GameServers\7DaysToDie";

    // Derived — not stored separately
    public string SteamCmdExe => System.IO.Path.Combine(DefaultInstallRoot, "steamcmd", "steamcmd.exe");

    // Discord
    public string DiscordWebhookUrl { get; set; } = "";
    public bool DiscordOnStart { get; set; } = true;
    public bool DiscordOnStop { get; set; } = true;
    public bool DiscordOnRestart { get; set; } = true;
    public bool DiscordOnWipe { get; set; } = true;
    public bool DiscordOnUpdateAvailable { get; set; } = true;
}
