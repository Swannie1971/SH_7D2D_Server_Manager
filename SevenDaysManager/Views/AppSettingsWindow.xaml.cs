using System.Windows;
using SevenDaysManager.Services;

namespace SevenDaysManager.Views;

public partial class AppSettingsWindow : Window
{
    public AppSettingsWindow()
    {
        InitializeComponent();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyTitleBar(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        ((ViewModels.AppSettingsViewModel)DataContext).SavePaths();
        Close();
    }
}
