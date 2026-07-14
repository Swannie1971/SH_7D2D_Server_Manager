using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace SevenDaysManager.Controls;

/// <summary>
/// Full-window ambience: engineering grid, scanlines, horizon glows, a drifting scan
/// sweep and a vignette. Drop it into the top-level Grid of a window, spanning all
/// rows/columns, and it will sit behind nothing and above everything, hit-test-invisible.
///
/// The looping sweep is gated on the system's "show window animations" setting so it
/// doesn't burn CPU over RDP or annoy users who've turned animations off.
/// </summary>
public partial class HudAtmosphere : UserControl
{
    private Storyboard? _sweepStoryboard;

    public HudAtmosphere()
    {
        InitializeComponent();
        Loaded   += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += (_, _) => RestartSweep();
    }

    private static bool MotionAllowed =>
        SystemParameters.ClientAreaAnimation && !System.Windows.Forms.SystemInformation.TerminalServerSession;

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!MotionAllowed)
        {
            Sweep.Visibility = Visibility.Collapsed;
            return;
        }
        RestartSweep();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        _sweepStoryboard?.Stop(this);
        _sweepStoryboard = null;
    }

    // The band drifts from just above the top edge to just past the bottom, forever.
    private void RestartSweep()
    {
        if (!MotionAllowed || !IsLoaded || ActualHeight <= 0) return;

        _sweepStoryboard?.Stop(this);

        var anim = new DoubleAnimation
        {
            From           = -260,
            To             = ActualHeight,
            Duration       = TimeSpan.FromSeconds(9),
            RepeatBehavior = RepeatBehavior.Forever
        };

        Storyboard.SetTarget(anim, SweepShift);
        Storyboard.SetTargetProperty(anim, new PropertyPath("Y"));

        _sweepStoryboard = new Storyboard();
        _sweepStoryboard.Children.Add(anim);
        _sweepStoryboard.Begin(this, true);
    }
}
