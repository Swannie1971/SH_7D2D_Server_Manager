using System.Windows;
using System.Windows.Input;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class AddServerWindow : Window
{
    public AddServerWindow()
    {
        InitializeComponent();
        DataContext = new AddServerViewModel();
    }

    // Borderless window: the header bar is the only thing left to drag by.
    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (AddServerViewModel)DataContext;
        if (vm.HasValidationError(out var msg))
        {
            HudDialog.Show(msg, "Invalid server", MessageBoxButton.OK, MessageBoxImage.Warning, owner: this);
            return;
        }
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e) =>
        DialogResult = false;

    public SevenDaysManager.Models.Server? CreatedServer =>
        DialogResult == true
            ? ((AddServerViewModel)DataContext).BuildServer()
            : null;
}
