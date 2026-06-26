using System.Windows;
using System.Windows.Media;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public static class CardBrushService
{
    public static readonly IReadOnlyList<CardColorSwatch> Swatches = new[]
    {
        // Greyscale
        new CardColorSwatch("Charcoal",    "1E1E1E"),
        new CardColorSwatch("Graphite",    "2D2D2D"),
        new CardColorSwatch("Black",       "080808"),
        // Blues & teals
        new CardColorSwatch("Steel",       "1C2B38"),
        new CardColorSwatch("Midnight",    "0D1520"),
        new CardColorSwatch("Dark Navy",   "080E1A"),
        new CardColorSwatch("Dark Teal",   "0D2525"),
        new CardColorSwatch("Dark Cyan",   "082020"),
        // Greens
        new CardColorSwatch("Dark Green",  "0A1F0E"),
        new CardColorSwatch("Forest",      "152515"),
        new CardColorSwatch("Moss",        "1A2010"),
        // Purples & pinks
        new CardColorSwatch("Dark Purple", "1A0D2E"),
        new CardColorSwatch("Violet",      "150D25"),
        new CardColorSwatch("Dark Plum",   "200A20"),
        // Reds & warm
        new CardColorSwatch("Dark Red",    "220808"),
        new CardColorSwatch("Burgundy",    "1F0A14"),
        new CardColorSwatch("Dark Brown",  "1F1208"),
        new CardColorSwatch("Espresso",    "261400"),
    };

    public static void Apply(string hexColor, int opacityPercent)
    {
        try
        {
            var r = Convert.ToByte(hexColor.Substring(0, 2), 16);
            var g = Convert.ToByte(hexColor.Substring(2, 2), 16);
            var b = Convert.ToByte(hexColor.Substring(4, 2), 16);
            var a = (byte)Math.Clamp(opacityPercent * 255 / 100, 0, 255);
            var brush = new SolidColorBrush(Color.FromArgb(a, r, g, b));
            brush.Freeze();
            Application.Current.Resources["GlassBrush"] = brush;
        }
        catch { /* bad hex — leave current brush unchanged */ }
    }

    public static void Apply(AppSettings settings) =>
        Apply(settings.CardColor, settings.CardOpacity);
}

public record CardColorSwatch(string Name, string Hex)
{
    public SolidColorBrush Brush { get; } =
        new(Color.FromRgb(
            Convert.ToByte(Hex.Substring(0, 2), 16),
            Convert.ToByte(Hex.Substring(2, 2), 16),
            Convert.ToByte(Hex.Substring(4, 2), 16)));
}
