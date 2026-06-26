using System.Windows;
using System.Windows.Media.Imaging;
using SevenDaysManager.Services;
using SevenDaysManager.ViewModels;

namespace SevenDaysManager.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        Icon = BitmapFrame.Create(new Uri("pack://application:,,,/Assets/logo.png"));
        _vm = new MainViewModel();
        DataContext = _vm;

        if (App.DataStore.GetAppSettings().StartMinimized)
            WindowState = WindowState.Minimized;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        // Palette already applied in App.OnStartup — just sync the title bar colour here
        ThemeService.ApplyTitleBar(this);
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
        var before = _vm.InstallBuildLabel;
        new InstallProgressWindow(server) { Owner = this }.ShowDialog();
        // OnServerUpdatedAsync calls RefreshInstallInfoAsync internally
        _ = _vm.OnServerUpdatedAsync(server);
    }

    private void CopyErrorLog_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrEmpty(_vm.ActionError))
            Clipboard.SetText(_vm.ActionError);
    }
}
