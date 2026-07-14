using System.Windows;
using System.Windows.Controls;

namespace SevenDaysManager.Controls;

/// <summary>Page title in the RustPanel idiom: "// DASHBOARD ▊" with a blinking cursor.</summary>
public partial class HudTitle : UserControl
{
    public HudTitle() => InitializeComponent();

    public static new readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(HudTitle),
            new PropertyMetadata(""));

    public new string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }
}
