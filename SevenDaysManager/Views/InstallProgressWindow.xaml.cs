using System.Windows;
using System.Windows.Input;
using SevenDaysManager.Models;
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

    // Borderless window: the header bar is the only thing left to drag by.
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
