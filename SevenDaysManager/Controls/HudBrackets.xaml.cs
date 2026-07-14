using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SevenDaysManager.Controls;

/// <summary>
/// The panel outline. Values here were MEASURED from the RustPanel reference screenshots by
/// sampling pixels — do not "improve" them from memory:
///
///   • The outline is a COMPLETE, CONTINUOUS 1px border, not corner brackets. Sampling the
///     top edge of a reference tile shows an unbroken run of 500+px at a constant colour.
///
///   • The border is DARK and DESATURATED — roughly half the brightness of the palette hue.
///     Measured border colours vs. their palette equivalents:
///
///         green   #267F49   (palette Green  #22C55E)
///         lime    #7B973D   (palette Accent #C1F24A)
///         blue    #355EA1   (palette Blue   #3B82F6)
///         amber   #9E6D1E   (palette Amber  #F59E0B)
///         violet  #6E619E   (palette Violet #A78BFA)
///
///     That is consistently ~62% of the palette value. Drawing the border at full saturation
///     is what made earlier attempts read as neon piping.
///
///   • No glow. The border is etched, not emissive.
/// </summary>
public partial class HudBrackets : UserControl
{
    /// <summary>
    /// How far the border colour is pulled down from the full palette hue.
    /// Derived from the reference: measured borders average ~62% of the palette RGB.
    /// </summary>
    private const double BorderLevel = 0.62;

    /// <summary>Corner ticks sit slightly brighter than the run of the border.</summary>
    private const double CornerLevel = 0.85;

    public HudBrackets()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateBrushes();
    }

    public static readonly DependencyProperty TintProperty =
        DependencyProperty.Register(nameof(Tint), typeof(HudTint), typeof(HudBrackets),
            new PropertyMetadata(HudTint.Accent, (o, _) => ((HudBrackets)o).UpdateBrushes()));

    public HudTint Tint
    {
        get => (HudTint)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    public static readonly DependencyProperty ArmProperty =
        DependencyProperty.Register(nameof(Arm), typeof(double), typeof(HudBrackets),
            new PropertyMetadata(20d));

    /// <summary>Corner-tick length in px.</summary>
    public double Arm
    {
        get => (double)GetValue(ArmProperty);
        set => SetValue(ArmProperty, value);
    }

    private static readonly DependencyPropertyKey EdgeBrushKey =
        DependencyProperty.RegisterReadOnly(nameof(EdgeBrush), typeof(Brush), typeof(HudBrackets),
            new PropertyMetadata(null));
    public static readonly DependencyProperty EdgeBrushProperty = EdgeBrushKey.DependencyProperty;

    /// <summary>The continuous border colour — the tint at ~62% brightness.</summary>
    public Brush? EdgeBrush => (Brush?)GetValue(EdgeBrushProperty);

    private static readonly DependencyPropertyKey CornerBrushKey =
        DependencyProperty.RegisterReadOnly(nameof(CornerBrush), typeof(Brush), typeof(HudBrackets),
            new PropertyMetadata(null));
    public static readonly DependencyProperty CornerBrushProperty = CornerBrushKey.DependencyProperty;

    /// <summary>The corner ticks — same hue, a little brighter than the border run.</summary>
    public Brush? CornerBrush => (Brush?)GetValue(CornerBrushProperty);

    private void UpdateBrushes()
    {
        var hue = HudTints.ToColor(Tint);

        // Neutral's hue IS BorderStrong (#343941), which is already dark. Scaling it down like
        // a saturated colour would drop it to ~#20232B — invisible against the panel fill, so
        // a Neutral panel would appear to have no brackets at all. Keep it at full strength.
        var isNeutral = Tint == HudTint.Neutral;

        var edge = new SolidColorBrush(isNeutral ? hue : HudTints.Scale(hue, BorderLevel));
        edge.Freeze();
        SetValue(EdgeBrushKey, edge);

        // Corner ticks always read brighter than the run of the border.
        var corner = new SolidColorBrush(isNeutral
            ? HudTints.Lighten(hue, 0.35)
            : HudTints.Scale(hue, CornerLevel));
        corner.Freeze();
        SetValue(CornerBrushKey, corner);
    }
}

/// <summary>Shared tint→colour mapping used by HudPanel, HudBrackets and HudStatusDot.</summary>
public static class HudTints
{
    public static readonly Color BorderStrong = Color.FromRgb(0x34, 0x39, 0x41);

    public static Color ToColor(HudTint tint) => tint switch
    {
        HudTint.Accent => Color.FromRgb(0xC1, 0xF2, 0x4A),
        HudTint.Rust   => Color.FromRgb(0xD9, 0x77, 0x57),
        HudTint.Green  => Color.FromRgb(0x22, 0xC5, 0x5E),
        HudTint.Amber  => Color.FromRgb(0xF5, 0x9E, 0x0B),
        HudTint.Red    => Color.FromRgb(0xEF, 0x44, 0x44),
        HudTint.Blue   => Color.FromRgb(0x3B, 0x82, 0xF6),
        HudTint.Violet => Color.FromRgb(0xA7, 0x8B, 0xFA),
        _              => BorderStrong,
    };

    /// <summary>Darken a hue toward black by a factor (0..1), preserving its character.</summary>
    public static Color Scale(Color c, double level)
    {
        var l = Math.Clamp(level, 0, 1);
        return Color.FromRgb((byte)(c.R * l), (byte)(c.G * l), (byte)(c.B * l));
    }

    /// <summary>Lift a colour toward white by a factor (0..1).</summary>
    public static Color Lighten(Color c, double amount)
    {
        var a = Math.Clamp(amount, 0, 1);
        return Color.FromRgb(
            (byte)(c.R + (255 - c.R) * a),
            (byte)(c.G + (255 - c.G) * a),
            (byte)(c.B + (255 - c.B) * a));
    }

    public static Color Mix(Color a, Color b, double aWeight)
    {
        var w = Math.Clamp(aWeight, 0, 1);
        return Color.FromRgb(
            (byte)(a.R * w + b.R * (1 - w)),
            (byte)(a.G * w + b.G * (1 - w)),
            (byte)(a.B * w + b.B * (1 - w)));
    }
}
