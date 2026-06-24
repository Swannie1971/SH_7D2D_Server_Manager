using System.Windows;
using MaterialDesignThemes.Wpf;
using SevenDaysManager.Services;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class AddServerWindow : Window
{
    public AddServerWindow()
    {
        InitializeComponent();
        DataContext = new AddServerViewModel();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyTitleBar(this);
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var vm = (AddServerViewModel)DataContext;
        if (vm.HasValidationError(out var msg))
        {
            var snackbar = new SnackbarMessageQueue(TimeSpan.FromSeconds(3));
            snackbar.Enqueue(msg);
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
