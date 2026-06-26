using Microsoft.Win32;

namespace SevenDaysManager.Services;

public static class AutoStartService
{
    private const string RegKey  = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "7D2D Server Manager";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegKey);
        return key?.GetValue(AppName) is not null;
    }

    public static bool Enable()
    {
        var exe = GetExePath();
        if (exe is null) return false;

        using var key = Registry.CurrentUser.OpenSubKey(RegKey, writable: true);
        key?.SetValue(AppName, $"\"{exe}\"");
        return key is not null;
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegKey, writable: true);
        key?.DeleteValue(AppName, throwOnMissingValue: false);
    }

    // Returns null when running via `dotnet run` (dev mode) — only works for published builds
    public static string? GetExePath()
    {
        var path = Environment.ProcessPath;
        if (path is null || path.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            return null;
        return path;
    }
}
