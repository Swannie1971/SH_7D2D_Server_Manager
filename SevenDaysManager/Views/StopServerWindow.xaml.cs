using System.Windows;
using System.Windows.Input;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class StopServerWindow : Window
{
    public StopServerWindow()
    {
        InitializeComponent();
        DataContext = new StopServerViewModel();
    }

    private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void StopButton_Click(object sender, RoutedEventArgs e) => DialogResult = true;

    private void CancelButton_Click(object sender, RoutedEventArgs e) => DialogResult = false;

    public int DelaySeconds => ((StopServerViewModel)DataContext).GetDelaySeconds();

    public string Message => ((StopServerViewModel)DataContext).Message?.Trim() ?? "";
}
