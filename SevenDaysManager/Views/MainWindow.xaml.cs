using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using SevenDaysManager.Services;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly NotifyIcon _trayIcon;
    private bool _hasShownTrayBalloon;

    public MainWindow()
    {
        InitializeComponent();
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/logo.png"));
        _vm = new MainViewModel();
        DataContext = _vm;

        _trayIcon = BuildTrayIcon();

        if (App.DataStore.GetAppSettings().StartMinimized)
            Hide();
    }

    private NotifyIcon BuildTrayIcon()
    {
        System.Drawing.Icon? appIcon = null;
        try
        {
            var stream = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/Assets/logo.ico"))?.Stream;
            if (stream != null)
                appIcon = new System.Drawing.Icon(stream);
        }
        catch { /* fall back to default */ }

        var menu = new ContextMenuStrip();

        var openItem = new ToolStripMenuItem("Open 7D2D Manager");
        openItem.Font = new System.Drawing.Font(openItem.Font, System.Drawing.FontStyle.Bold);
        openItem.Click += (_, _) => ShowMainWindow();

        var exitItem = new ToolStripMenuItem("Exit");
        exitItem.Click += (_, _) => ExitApp();

        menu.Items.Add(openItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(exitItem);

        var icon = new NotifyIcon
        {
            Icon = appIcon ?? System.Drawing.SystemIcons.Application,
            Text = "7D2D Server Manager",
            Visible = true,
            ContextMenuStrip = menu
        };
        icon.DoubleClick += (_, _) => ShowMainWindow();

        return icon;
    }

    private void ShowMainWindow()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void ExitApp()
    {
        _trayIcon.Visible = false;
        _trayIcon.Dispose();
        System.Windows.Application.Current.Shutdown();
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        e.Cancel = true;
        Hide();
        if (!_hasShownTrayBalloon)
        {
            _hasShownTrayBalloon = true;
            _trayIcon.ShowBalloonTip(
                3000,
                "Still running",
                "7D2D Server Manager is minimised to the tray.\nRight-click the icon to exit.",
                ToolTipIcon.Info);
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        ThemeService.ApplyTitleBar(this);
        _ = CheckForUpdatesAsync();
    }

    private async Task CheckForUpdatesAsync()
    {
        var update = await UpdateService.CheckAsync();
        if (update is null) return;
        await Dispatcher.InvokeAsync(() =>
            new UpdateAvailableWindow(update) { Owner = this }.ShowDialog());
    }

    private void AddServerButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new AddServerWindow { Owner = this };
        if (dialog.ShowDialog() == true && dialog.CreatedServer is { } server)
            _vm.AddServer(server);
    }

    private void AppSettingsButton_Click(object sender, RoutedEventArgs e)
    {
        new AppSettingsWindow { Owner = this }.ShowDialog();
    }

    private void OverviewCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenOverviewCommand.Execute(null);

    private void PlayersCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenPlayersCommand.Execute(null);

    private void BackupsCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenBackupsCommand.Execute(null);

    private void ScheduleCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenScheduleCommand.Execute(null);

    private void DiscordCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenDiscordCommand.Execute(null);

    private void ModsCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenModsCommand.Execute(null);

    private void ServerSettingsCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenServerSettingsCommand.Execute(null);

    private void ConsoleCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenConsoleCommand.Execute(null);

    private void ConfigCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenConfigCommand.Execute(null);

    private void GameSettingsCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) =>
        _vm.OpenGameSettingsCommand.Execute(null);

    private void InstallCard_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_vm.SelectedServer is not { } server) return;

        // Don't allow install/update while the server is running — SteamCMD would fight file locks.
        if (_vm.SelectedServerRunning)
        {
            MessageBox.Show(this,
                "The server is currently running. Stop it before installing or updating.",
                "Server is running",
                MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        new InstallProgressWindow(server) { Owner = this }.ShowDialog();
        _ = _vm.OnServerUpdatedAsync(server);
    }

    private void CopyErrorLog_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_vm.ActionError))
            Clipboard.SetText(_vm.ActionError);
    }
}
