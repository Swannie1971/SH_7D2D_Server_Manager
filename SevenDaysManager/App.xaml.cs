using System.Windows;
using SevenDaysManager.Services;

namespace SevenDaysManager;

public partial class App : Application
{
    public static DataStore DataStore { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DataStore = new DataStore();

        DispatcherUnhandledException += (_, ex) =>
        {
            System.Windows.MessageBox.Show(
                $"{ex.Exception.GetType().Name}: {ex.Exception.Message}\n\n{ex.Exception.StackTrace}",
                "Unhandled Exception", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
            ex.Handled = true;
        };
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DataStore.Dispose();
        base.OnExit(e);
    }
}
