using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SevenDaysManager.Controls;

/// <summary>
/// A stroked line icon: 24×24 geometry drawn as an OUTLINE with flat caps and miter
/// joins. Drop-in replacement for MaterialDesign's PackIcon, whose geometry is filled
/// and rounded — the two things the tactical style forbids.
///
/// <para>Usage: <c>&lt;c:HudIcon Kind="ContentSave" Width="13" Height="13"
/// Foreground="{DynamicResource Hud.Accent}"/&gt;</c></para>
///
/// Geometry lives in Theme/HudIcons.xaml as <c>Icon.{Kind}</c>. An unknown Kind draws
/// nothing rather than throwing, so a typo degrades to a blank space instead of taking
/// the window down at parse time.
/// </summary>
public class HudIcon : System.Windows.Controls.Control
{
    /// <summary>The viewbox every icon geometry is authored against.</summary>
    private const double DesignSize = 24.0;

    /// <summary>Target on-screen stroke width, per the design spec.</summary>
    private const double DesignStroke = 1.5;

    static HudIcon()
    {
        DefaultStyleKeyProperty.OverrideMetadata(
            typeof(HudIcon), new FrameworkPropertyMetadata(typeof(HudIcon)));
    }

    public static readonly DependencyProperty KindProperty =
        DependencyProperty.Register(nameof(Kind), typeof(string), typeof(HudIcon),
            new PropertyMetadata(null, OnKindChanged));

    /// <summary>Icon name, matching an <c>Icon.{Kind}</c> key in Theme/HudIcons.xaml.</summary>
    public string? Kind
    {
        get => (string?)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    private static readonly DependencyPropertyKey DataKey =
        DependencyProperty.RegisterReadOnly(nameof(Data), typeof(Geometry), typeof(HudIcon),
            new PropertyMetadata(null));
    public static readonly DependencyProperty DataProperty = DataKey.DependencyProperty;

    /// <summary>Resolved geometry for the current <see cref="Kind"/>. Bound by the template.</summary>
    public Geometry? Data => (Geometry?)GetValue(DataProperty);

    private static readonly DependencyPropertyKey ScaledStrokeKey =
        DependencyProperty.RegisterReadOnly(nameof(ScaledStroke), typeof(double), typeof(HudIcon),
            new PropertyMetadata(DesignStroke));
    public static readonly DependencyProperty ScaledStrokeProperty = ScaledStrokeKey.DependencyProperty;

    /// <summary>
    /// StrokeThickness to hand the Path, pre-divided by the scale factor.
    ///
    /// The Path uses Stretch="Uniform", which scales the *stroke* along with the geometry.
    /// At a typical 13px icon that would render a 1.5px stroke as 1.5 × (13/24) ≈ 0.8px —
    /// visibly thin and washed out, and inconsistent between a 13px button icon and a 19px
    /// header icon. Dividing by the same factor here cancels it out, so every icon lands on
    /// ~1.5px on screen regardless of size.
    /// </summary>
    public double ScaledStroke => (double)GetValue(ScaledStrokeKey.DependencyProperty);

    private static void OnKindChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) =>
        ((HudIcon)d).ResolveGeometry();

    public HudIcon()
    {
        // Size is usually set by the caller after construction, and Foreground may arrive
        // via inheritance, so recompute once we're actually in the tree.
        Loaded += (_, _) =>
        {
            ResolveGeometry();
            UpdateStroke();
        };
        SizeChanged += (_, _) => UpdateStroke();
    }

    private void ResolveGeometry()
    {
        if (string.IsNullOrEmpty(Kind))
        {
            SetValue(DataKey, null);
            return;
        }

        // TryFindResource, not FindResource: an unknown Kind should draw nothing, not throw.
        var geo = TryFindResource($"Icon.{Kind}") as Geometry;
        SetValue(DataKey, geo);
    }

    private void UpdateStroke()
    {
        var size = Math.Min(
            double.IsNaN(Width)  || Width  <= 0 ? ActualWidth  : Width,
            double.IsNaN(Height) || Height <= 0 ? ActualHeight : Height);

        if (size <= 0) return;

        var scale = size / DesignSize;
        SetValue(ScaledStrokeKey, scale > 0 ? DesignStroke / scale : DesignStroke);
    }
}
