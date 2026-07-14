using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace SevenDaysManager.Converters;

/// <summary>
/// Ramps a 0..1 severity through the HUD palette: green (safe) → amber → red (lethal).
///
/// Used by the difficulty preset buttons so the scale is readable at a glance — you shouldn't
/// have to already know that "True Survivalist" is harder than "Warrior" to see it.
///
/// The midpoint is amber rather than a straight green→red lerp, which would pass through a
/// muddy olive around the middle. Two segments keep every stop a colour that exists in the
/// palette.
///
/// <para>Parameter selects the variant:
/// <list type="bullet">
///   <item><c>"fill"</c> — faint translucent wash, for an unselected button's background.</item>
///   <item><c>"solid"</c> — full-strength, for the SELECTED button's background.</item>
///   <item><c>"ink"</c> — near-black, for text sitting on a "solid" fill.</item>
///   <item>anything else — the plain colour, for borders and text.</item>
/// </list></para>
/// </summary>
public sealed class SeverityToBrushConverter : IValueConverter
{
    // The palette's own green / amber / red.
    private static readonly Color Safe    = Color.FromRgb(0x22, 0xC5, 0x5E);
    private static readonly Color Caution = Color.FromRgb(0xF5, 0x9E, 0x0B);
    private static readonly Color Lethal  = Color.FromRgb(0xEF, 0x44, 0x44);

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        var t = value switch
        {
            double d => d,
            int i    => i,
            _        => 0d,
        };
        t = Math.Clamp(t, 0, 1);

        // Two segments so the middle of the ramp is amber, not olive.
        var color = t <= 0.5
            ? Lerp(Safe,    Caution, t * 2)
            : Lerp(Caution, Lethal, (t - 0.5) * 2);

        switch (parameter as string)
        {
            case "fill":
                // Faint wash; the border carries the signal.
                color.A = 0x22;
                break;

            case "ink":
                // Text to sit ON a "solid" fill. The ramp spans green → amber → red, all of
                // which are light enough that a near-black reads cleanly — whereas the theme's
                // AccentInk is tuned for the bright accent green and disappears against the
                // muted end of the ramp.
                color = Color.FromRgb(0x0A, 0x0B, 0x0D);
                break;
        }

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Color Lerp(Color a, Color b, double t) => Color.FromArgb(
        0xFF,
        (byte)(a.R + (b.R - a.R) * t),
        (byte)(a.G + (b.G - a.G) * t),
        (byte)(a.B + (b.B - a.B) * t));

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
