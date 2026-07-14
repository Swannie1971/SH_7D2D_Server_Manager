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

        // Restore saved theme — without this the app always resets to the default on restart
        var appSettings = DataStore.GetAppSettings();
        Services.ThemeService.Apply(Services.ThemeService.Load());
        Services.CardBrushService.Apply(appSettings);
        Services.BackgroundImageService.Apply(appSettings);
        Services.FontService.Apply(appSettings);

        DispatcherUnhandledException += OnUnhandled;
    }

    private static bool _showingError;

    /// <summary>
    /// Show the first unhandled exception, then get out of the way.
    ///
    /// The previous version popped a MessageBox on EVERY dispatcher exception. MessageBox
    /// pumps the message loop, so an exception thrown during layout would re-enter layout,
    /// throw again, pop another MessageBox... recursing until the stack overflowed. The real
    /// error was buried under hundreds of frames of MessageBox.Show. The re-entrancy guard
    /// keeps the first (actual) error visible.
    /// </summary>
    private static void OnUnhandled(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
    {
        e.Handled = true;

        if (_showingError) return;   // never recurse — see above
        _showingError = true;

        // Always write the full detail somewhere we can read it, even if the UI is too broken
        // to show a dialog.
        var detail = $"{e.Exception.GetType().FullName}: {e.Exception.Message}\n\n{e.Exception}";
        try
        {
            var log = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "7D2DManager", "crash.log");
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(log)!);
            System.IO.File.WriteAllText(log, detail);
        }
        catch { /* logging must never itself throw */ }

        try
        {
            System.Windows.MessageBox.Show(detail, "Unhandled Exception",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Error);
        }
        catch { /* UI may be unusable */ }

        Current?.Shutdown(1);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        DataStore.Dispose();
        base.OnExit(e);
    }
}
