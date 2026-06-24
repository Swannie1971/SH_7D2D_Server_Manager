using System.Windows;
using SevenDaysManager.Models;
using SevenDaysManager.Services;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class ConfigWindow : Window
{
    public ConfigWindow(Server server)
    {
        InitializeComponent();
        DataContext = new ConfigViewModel(server);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyTitleBar(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
