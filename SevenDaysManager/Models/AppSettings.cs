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

    // Card appearance
    public string CardColor   { get; set; } = "1E1E1E";  // hex RGB, no #
    public int    CardOpacity { get; set; } = 50;         // 0–100 %
}
