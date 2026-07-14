using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using MaterialDesignColors;
using MaterialDesignThemes.Wpf;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public static class ThemeService
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

    private const int DWMWA_CAPTION_COLOR = 35;

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "7D2DManager", "theme.json");

    public static AppThemeSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                return JsonSerializer.Deserialize<AppThemeSettings>(json) ?? new();
            }
        }
        catch { }
        return new();
    }

    // The tactical HUD palette is fixed — the title bar is always the elevated abyss
    // (#0B0C0E) so the OS chrome disappears into the window.
    private static readonly Color TitleBarColor = Color.FromRgb(0x0B, 0x0C, 0x0E);

    public static void Apply(AppThemeSettings settings)
    {
        // Palette is fixed by the HUD theme dictionaries; nothing to recolour at runtime.
        // We keep the settings round-trip so existing saved files stay valid.
        Save(settings);
        UpdateAllTitleBars(TitleBarColor);
    }

    // Paint a single window's title bar to match the abyss (call from OnSourceInitialized)
    public static void ApplyTitleBar(Window w)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;
        int bgr = TitleBarColor.R | (TitleBarColor.G << 8) | (TitleBarColor.B << 16);
        var hwnd = new WindowInteropHelper(w).Handle;
        if (hwnd != IntPtr.Zero)
            DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref bgr, 4);
    }

    public static void UpdateAllTitleBars(Color color)
    {
        if (!OperatingSystem.IsWindowsVersionAtLeast(10, 0, 22000)) return;

        int bgr = color.R | (color.G << 8) | (color.B << 16);

        foreach (Window w in Application.Current.Windows)
        {
            var hwnd = new WindowInteropHelper(w).Handle;
            if (hwnd != IntPtr.Zero)
                DwmSetWindowAttribute(hwnd, DWMWA_CAPTION_COLOR, ref bgr, 4);
        }
    }

    public static IEnumerable<SwatchInfo> GetSwatches()
    {
        return new SwatchesProvider().Swatches
            .Select(s => new SwatchInfo(s.Name, s.ExemplarHue.Color));
    }

    private static bool TryGetSwatchColor(string name, out Color color)
    {
        var swatch = new SwatchesProvider().Swatches
            .FirstOrDefault(s => s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

        if (swatch is not null)
        {
            color = swatch.ExemplarHue.Color;
            return true;
        }

        color = default;
        return false;
    }

    private static void Save(AppThemeSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(settings,
            new JsonSerializerOptions { WriteIndented = true }));
    }
}

public record SwatchInfo(string Name, Color Color);
