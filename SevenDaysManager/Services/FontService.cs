using System.Windows;
using System.Windows.Media;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

/// <summary>
/// Swaps the display face used by headers and titles (Hud.PanelHeader, Hud.PageTitle,
/// Hud.NavCard.Title). Data text is never touched — ports, IPs, counts, timestamps and log
/// output stay mono, because a proportional face makes columns of digits ragged.
///
/// The header styles reference Hud.Display and Hud.Display.Tracking as DynamicResource, so
/// replacing those two entries restyles every header live, with no restart.
/// </summary>
public static class FontService
{
    /// <summary>
    /// The faces offered in Settings, with the letter-spacing each one actually wants.
    ///
    /// Tracking is per-face on purpose. The condensed faces (Impact) need far more air than
    /// the wide ones or they read as a solid wall; swapping the family alone would leave
    /// Impact looking cramped and Rockwell looking sprawled.
    ///
    /// Every face here is a Windows built-in and was verified to pass TryGetGlyphTypeface —
    /// TrackedText resolves a single real glyph typeface to build its GlyphRun, so a face
    /// that fails that would break the tracking outright.
    /// </summary>
    public static readonly IReadOnlyList<FontOption> Options = new[]
    {
        new FontOption("Rockwell",   "Rockwell",   0.14, "Slab serif — industrial, solid"),
        new FontOption("Stencil",    "Stencil",    0.14, "Military stencil — closest to the spec"),
        new FontOption("Agency FB",  "Agency FB",  0.16, "Narrow techno — reads as instrumentation"),
        new FontOption("Impact",     "Impact",     0.20, "Heavy condensed — needs wide tracking"),
    };

    public record FontOption(string Label, string Family, double Tracking, string Note);

    private const string FallbackChain = ", Bahnschrift, Segoe UI";

    public static void Apply(string? family)
    {
        var opt = Options.FirstOrDefault(o =>
                      string.Equals(o.Family, family, StringComparison.OrdinalIgnoreCase))
                  ?? Options[0];

        // Keep a fallback chain: if the face is somehow missing on this machine, WPF walks
        // down it rather than silently dropping to a default that ignores our metrics.
        var ff = new FontFamily(opt.Family + FallbackChain);

        Application.Current.Resources["Hud.Display"]          = ff;
        Application.Current.Resources["Hud.Display.Tracking"] = opt.Tracking;
    }

    public static void Apply(AppSettings settings) => Apply(settings.DisplayFont);
}
