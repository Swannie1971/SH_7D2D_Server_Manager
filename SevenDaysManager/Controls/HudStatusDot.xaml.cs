using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace SevenDaysManager.Controls;

/// <summary>
/// A square status indicator. When <see cref="IsLive"/> is true it glows and emits an
/// expanding "radar ping" ring on a ~2s loop; when false it's a flat, dead FgFaint square.
/// </summary>
public partial class HudStatusDot : UserControl
{
    private Storyboard? _ping;

    public HudStatusDot()
    {
        InitializeComponent();
        Loaded += (_, _) => Refresh();
    }

    public static readonly DependencyProperty StatusProperty =
        DependencyProperty.Register(nameof(Status), typeof(HudTint), typeof(HudStatusDot),
            new PropertyMetadata(HudTint.Green, (o, _) => ((HudStatusDot)o).Refresh()));

    /// <summary>Colour of the dot when live.</summary>
    public HudTint Status
    {
        get => (HudTint)GetValue(StatusProperty);
        set => SetValue(StatusProperty, value);
    }

    public static readonly DependencyProperty IsLiveProperty =
        DependencyProperty.Register(nameof(IsLive), typeof(bool), typeof(HudStatusDot),
            new PropertyMetadata(false, (o, _) => ((HudStatusDot)o).Refresh()));

    /// <summary>When true: coloured + glowing + pinging. When false: flat and dead.</summary>
    public bool IsLive
    {
        get => (bool)GetValue(IsLiveProperty);
        set => SetValue(IsLiveProperty, value);
    }

    private static readonly DependencyPropertyKey DotBrushKey =
        DependencyProperty.RegisterReadOnly(nameof(DotBrush), typeof(Brush), typeof(HudStatusDot),
            new PropertyMetadata(null));
    public static readonly DependencyProperty DotBrushProperty = DotBrushKey.DependencyProperty;

    public Brush? DotBrush => (Brush?)GetValue(DotBrushProperty);

    private static bool MotionAllowed =>
        SystemParameters.ClientAreaAnimation && !System.Windows.Forms.SystemInformation.TerminalServerSession;

    private void Refresh()
    {
        if (!IsLoaded) return;

        var color = IsLive
            ? Status switch
            {
                HudTint.Accent => Color.FromRgb(0xC1, 0xF2, 0x4A),
                HudTint.Rust   => Color.FromRgb(0xD9, 0x77, 0x57),
                HudTint.Green  => Color.FromRgb(0x22, 0xC5, 0x5E),
                HudTint.Amber  => Color.FromRgb(0xF5, 0x9E, 0x0B),
                HudTint.Red    => Color.FromRgb(0xEF, 0x44, 0x44),
                HudTint.Blue   => Color.FromRgb(0x3B, 0x82, 0xF6),
                HudTint.Violet => Color.FromRgb(0xA7, 0x8B, 0xFA),
                _              => Color.FromRgb(0x4D, 0x54, 0x5B),
            }
            : Color.FromRgb(0x4D, 0x54, 0x5B); // FgFaint — offline

        var brush = new SolidColorBrush(color);
        brush.Freeze();
        SetValue(DotBrushKey, brush);

        // Glow only when live
        Dot.Effect = IsLive
            ? new DropShadowEffect { Color = color, BlurRadius = 9, ShadowDepth = 0, Opacity = 0.85 }
            : null;

        _ping?.Stop(this);
        _ping = null;
        Ping.Opacity = 0;

        if (!IsLive || !MotionAllowed) return;

        // Expanding ring: scale 1 -> 2.4 while fading out, forever.
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever };

        var sx = new DoubleAnimation(1, 2.4, TimeSpan.FromSeconds(2));
        Storyboard.SetTarget(sx, PingScale);
        Storyboard.SetTargetProperty(sx, new PropertyPath("ScaleX"));
        sb.Children.Add(sx);

        var sy = new DoubleAnimation(1, 2.4, TimeSpan.FromSeconds(2));
        Storyboard.SetTarget(sy, PingScale);
        Storyboard.SetTargetProperty(sy, new PropertyPath("ScaleY"));
        sb.Children.Add(sy);

        var fade = new DoubleAnimation(0.9, 0, TimeSpan.FromSeconds(2));
        Storyboard.SetTarget(fade, Ping);
        Storyboard.SetTargetProperty(fade, new PropertyPath("Opacity"));
        sb.Children.Add(fade);

        _ping = sb;
        sb.Begin(this, true);
    }
}
