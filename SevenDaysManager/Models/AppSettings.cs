namespace SevenDaysManager.Models;

public class AppSettings
{
    public string Id { get; set; } = "singleton";

    // Paths
    public string DefaultInstallRoot { get; set; } = @"C:\GameServers\7DaysToDie";

    // SteamCMD is shared across every server, so it lives in a reserved sibling folder of
    // DefaultInstallRoot (SteamCmdService.ReservedFolderName) rather than inside any one
    // server's own folder — see SteamCmdService.SharedSteamCmdDir for why that distinction
    // matters. Built from THIS instance's DefaultInstallRoot, not a fresh settings read, so it
    // always matches whatever root this particular AppSettings object actually has.
    public string SteamCmdExe => System.IO.Path.Combine(
        DefaultInstallRoot, Services.SteamCmdService.ReservedFolderName, "steamcmd.exe");

    // Behaviour
    public bool StartMinimized { get; set; } = false;

    // Panel appearance. The HUD palette is fixed, so only opacity is user-controlled;
    // CardColor is retained purely so existing LiteDB documents still deserialize.
    public string CardColor   { get; set; } = "0E0F12";
    public int    CardOpacity { get; set; } = 92;         // 30–100 %

    // Empty = use the bundled default background
    public string BackgroundImagePath { get; set; } = "";

    // Display face for headers and titles. Data (ports, IPs, counts, logs) always stays mono.
    // Stored as the family name; see FontService for the allowed set and per-face tracking.
    public string DisplayFont { get; set; } = "Rockwell";
}
