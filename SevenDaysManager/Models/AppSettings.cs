namespace SevenDaysManager.Models;

public class AppSettings
{
    public string Id { get; set; } = "singleton";

    // Paths
    public string DefaultInstallRoot { get; set; } = @"C:\GameServers\7DaysToDie";

    // Derived — not stored separately
    public string SteamCmdExe => System.IO.Path.Combine(DefaultInstallRoot, "steamcmd", "steamcmd.exe");

    // Behaviour
    public bool StartMinimized { get; set; } = false;

    // Panel appearance. The HUD palette is fixed, so only opacity is user-controlled;
    // CardColor is retained purely so existing LiteDB documents still deserialize.
    public string CardColor   { get; set; } = "0E0F12";
    public int    CardOpacity { get; set; } = 92;         // 30–100 %

    // Empty = use the bundled default background
    public string BackgroundImagePath { get; set; } = "";
}
