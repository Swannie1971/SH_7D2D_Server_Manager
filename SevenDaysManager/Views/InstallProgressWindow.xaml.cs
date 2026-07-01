using System.Windows;
using SevenDaysManager.Models;
using SevenDaysManager.Services;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class InstallProgressWindow : Window
{
    private readonly InstallProgressViewModel _vm;

    public InstallProgressWindow(Server server)
    {
        InitializeComponent();
        _vm = new InstallProgressViewModel(server);
        DataContext = _vm;

        // Auto-scroll log as lines arrive
        _vm.LogLines.CollectionChanged += (_, _) =>
        {
            LogScroller.ScrollToBottom();
        };
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyTitleBar(this);
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
