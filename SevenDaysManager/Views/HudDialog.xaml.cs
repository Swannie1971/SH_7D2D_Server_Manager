using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using MaterialDesignThemes.Wpf;
using SevenDaysManager.Controls;

namespace SevenDaysManager.Views;

/// <summary>
/// A HUD-styled replacement for <see cref="System.Windows.MessageBox"/>.
///
/// The OS MessageBox is drawn by Windows, not WPF — it cannot be themed at all, so it always
/// looks like a stock system dialog no matter how the app is styled. This gives the same API
/// (Show → MessageBoxResult) but renders as a square, bracketed HUD panel.
///
/// The one place we deliberately still use the native MessageBox is the crash handler in
/// App.xaml.cs: if the UI is broken enough to throw during layout, it may not be able to render
/// our own dialog.
/// </summary>
public partial class HudDialog : Window
{
    private MessageBoxResult _result = MessageBoxResult.None;

    private HudDialog() => InitializeComponent();

    /// <summary>MessageBox-compatible entry point.</summary>
    public static MessageBoxResult Show(
        string message,
        string title,
        MessageBoxButton buttons = MessageBoxButton.OK,
        MessageBoxImage icon     = MessageBoxImage.None,
        Window?          owner   = null)
    {
        var dlg = new HudDialog();

        dlg.TitleText.Text   = title.ToUpperInvariant();
        dlg.MessageText.Text = message;
        dlg.ApplyIcon(icon);
        dlg.BuildButtons(buttons);

        // Owner keeps the dialog centred on the app and properly modal. Fall back to the active
        // window so this still works when called from a ViewModel with no Window reference.
        owner ??= Application.Current?.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                  ?? Application.Current?.MainWindow;

        if (owner is not null && owner != dlg && owner.IsLoaded)
            dlg.Owner = owner;
        else
            dlg.WindowStartupLocation = WindowStartupLocation.CenterScreen;

        dlg.ShowDialog();
        return dlg._result;
    }

    /// <summary>The icon also sets the accent: warnings amber, errors red, questions accent.</summary>
    private void ApplyIcon(MessageBoxImage icon)
    {
        var (kind, tint) = icon switch
        {
            MessageBoxImage.Error       => (PackIconKind.AlertOctagon,      HudTint.Red),
            MessageBoxImage.Warning     => (PackIconKind.AlertCircleOutline, HudTint.Amber),
            MessageBoxImage.Question    => (PackIconKind.HelpCircleOutline,  HudTint.Accent),
            MessageBoxImage.Information => (PackIconKind.InformationOutline, HudTint.Blue),
            _                           => (PackIconKind.ChevronRight,       HudTint.Accent),
        };

        TitleIcon.Kind = kind;

        var color = HudTints.ToColor(tint);
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        TitleIcon.Foreground = brush;

        Brackets.Tint = tint;
    }

    private void BuildButtons(MessageBoxButton buttons)
    {
        switch (buttons)
        {
            case MessageBoxButton.OK:
                AddButton("OK", MessageBoxResult.OK, primary: true, isDefault: true, isCancel: true);
                break;

            case MessageBoxButton.OKCancel:
                AddButton("CANCEL", MessageBoxResult.Cancel, primary: false, isCancel: true);
                AddButton("OK",     MessageBoxResult.OK,     primary: true,  isDefault: true);
                break;

            case MessageBoxButton.YesNo:
                AddButton("NO",  MessageBoxResult.No,  primary: false, isCancel: true);
                AddButton("YES", MessageBoxResult.Yes, primary: true,  isDefault: true);
                break;

            case MessageBoxButton.YesNoCancel:
                AddButton("CANCEL", MessageBoxResult.Cancel, primary: false, isCancel: true);
                AddButton("NO",     MessageBoxResult.No,     primary: false);
                AddButton("YES",    MessageBoxResult.Yes,    primary: true, isDefault: true);
                break;
        }
    }

    private void AddButton(string label, MessageBoxResult result, bool primary,
                           bool isDefault = false, bool isCancel = false)
    {
        var text = new TrackedText
        {
            Text          = label,
            LetterSpacing = 0.12,
            FontFamily    = (FontFamily)FindResource("Hud.Mono"),
            FontSize      = 10.5,
            FontWeight    = FontWeights.Bold,
            Foreground    = (Brush)FindResource(primary ? "Hud.AccentInk" : "Hud.Fg"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        var btn = new System.Windows.Controls.Button
        {
            Style     = (Style)FindResource(primary ? "Hud.Button.Primary" : "Hud.Button"),
            Content   = text,
            Height    = 30,
            MinWidth  = 92,
            Padding   = new Thickness(16, 0, 16, 0),
            Margin    = new Thickness(ButtonRow.Children.Count == 0 ? 0 : 8, 0, 0, 0),
            IsDefault = isDefault,
            IsCancel  = isCancel,
        };

        btn.Click += (_, _) => { _result = result; DialogResult = true; };
        ButtonRow.Children.Add(btn);
    }
}
