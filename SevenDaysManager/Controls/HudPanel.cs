using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SevenDaysManager.Controls;

/// <summary>
/// Tint options for a HudPanel — drives the corner brackets and border colour so panel
/// *types* can be colour-coded (danger = Red, info = Blue, etc).
/// </summary>
public enum HudTint
{
    Accent,
    Rust,
    Green,
    Amber,
    Red,
    Blue,
    Violet,
    Neutral
}

/// <summary>
/// The signature panel of the tactical HUD: a square-cornered, shadow-less surface marked
/// at its four corners by 1px L-shaped brackets, like a targeting reticle. On hover the
/// bracket arms extend (13 -> 22px) and turn full accent — "acquiring lock".
///
/// Content is set as normal (it's a ContentControl):
///     &lt;c:HudPanel Header="SERVER STATUS" Tint="Green"&gt; ...content... &lt;/c:HudPanel&gt;
/// </summary>
public class HudPanel : ContentControl
{
    static HudPanel()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HudPanel), new FrameworkPropertyMetadata(typeof(HudPanel)));
    }

    public static readonly DependencyProperty HeaderProperty =
        DependencyProperty.Register(nameof(Header), typeof(string), typeof(HudPanel),
            new PropertyMetadata(null));

    /// <summary>Optional uppercase-mono header row. Null/empty hides the header entirely.</summary>
    public string? Header
    {
        get => (string?)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public static readonly DependencyProperty TintProperty =
        DependencyProperty.Register(nameof(Tint), typeof(HudTint), typeof(HudPanel),
            new PropertyMetadata(HudTint.Accent, OnTintChanged));

    public HudTint Tint
    {
        get => (HudTint)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    // Resolved brushes the template binds to, so the ControlTemplate stays tint-agnostic.
    private static readonly DependencyPropertyKey TintBrushKey =
        DependencyProperty.RegisterReadOnly(nameof(TintBrush), typeof(Brush), typeof(HudPanel),
            new PropertyMetadata(null));
    public static readonly DependencyProperty TintBrushProperty = TintBrushKey.DependencyProperty;

    /// <summary>Full-strength tint colour — brackets on hover.</summary>
    public Brush? TintBrush => (Brush?)GetValue(TintBrushProperty);

    private static readonly DependencyPropertyKey BracketBrushKey =
        DependencyProperty.RegisterReadOnly(nameof(BracketBrush), typeof(Brush), typeof(HudPanel),
            new PropertyMetadata(null));
    public static readonly DependencyProperty BracketBrushProperty = BracketBrushKey.DependencyProperty;

    /// <summary>Resting panel outline — the tint muted ~38% into gunmetal.</summary>
    public Brush? BracketBrush => (Brush?)GetValue(BracketBrushProperty);

    private static readonly DependencyPropertyKey BracketRestKey =
        DependencyProperty.RegisterReadOnly(nameof(BracketRest), typeof(Brush), typeof(HudPanel),
            new PropertyMetadata(null));
    public static readonly DependencyProperty BracketRestProperty = BracketRestKey.DependencyProperty;

    /// <summary>Resting corner-bracket colour — brighter than the outline, dimmer than full tint.</summary>
    public Brush? BracketRest => (Brush?)GetValue(BracketRestProperty);

    public static readonly DependencyProperty ShowBracketsProperty =
        DependencyProperty.Register(nameof(ShowBrackets), typeof(bool), typeof(HudPanel),
            new PropertyMetadata(true));

    public bool ShowBrackets
    {
        get => (bool)GetValue(ShowBracketsProperty);
        set => SetValue(ShowBracketsProperty, value);
    }

    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        UpdateTintBrushes();
    }

    private static void OnTintChanged(DependencyObject o, DependencyPropertyChangedEventArgs e) =>
        ((HudPanel)o).UpdateTintBrushes();

    private void UpdateTintBrushes()
    {
        var color = HudTints.ToColor(Tint);

        var full = new SolidColorBrush(color);
        full.Freeze();
        SetValue(TintBrushKey, full);

        // Resting outline = tint mixed 38% into BorderStrong, so each panel carries a
        // visibly coloured — but muted — border of its own type.
        var rest = new SolidColorBrush(HudTints.Mix(color, HudTints.BorderStrong, 0.38));
        rest.Freeze();
        SetValue(BracketBrushKey, rest);

        // Corner brackets sit brighter than the outline even at rest — they're the
        // "reticle" that reads before the border does.
        var bracket = new SolidColorBrush(HudTints.Mix(color, HudTints.BorderStrong, 0.72));
        bracket.Freeze();
        SetValue(BracketRestKey, bracket);
    }
}
