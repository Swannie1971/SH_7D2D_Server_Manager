using System.Windows;
using System.Windows.Media;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

/// <summary>
/// The tactical HUD uses a fixed palette, so the user no longer picks a card colour —
/// panels are always the Surface colour. What the user *does* still control is the
/// panel opacity, so panels can act as translucent glass over a custom background image.
/// </summary>
public static class CardBrushService
{
    // Surface #0E0F12 — the fixed panel fill from the HUD palette.
    private const byte R = 0x0E, G = 0x0F, B = 0x12;

    /// <param name="opacityPercent">30–100. Below ~30 the text stops being legible.</param>
    public static void Apply(int opacityPercent)
    {
        var a = (byte)Math.Clamp(opacityPercent * 255 / 100, 0, 255);
        var brush = new SolidColorBrush(Color.FromArgb(a, R, G, B));
        brush.Freeze();
        Application.Current.Resources["GlassBrush"] = brush;
    }

    public static void Apply(AppSettings settings) => Apply(settings.CardOpacity);
}
