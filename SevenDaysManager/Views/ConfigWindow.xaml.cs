using System.Windows;
using System.Windows.Input;
using SevenDaysManager.Models;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class ConfigWindow : Window
{
    public ConfigWindow(Server server)
    {
        InitializeComponent();
        DataContext = new ConfigViewModel(server);
    }

    // Borderless window: the header bar is the only thing left to drag by.
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();
}
