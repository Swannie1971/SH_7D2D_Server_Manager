using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SevenDaysManager.Models;

namespace SevenDaysManager.ViewModels;

public partial class AddServerViewModel : ObservableObject
{
    // ── Form fields ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _name = "My Server";
    [ObservableProperty] private string _logoPath = "";
    [ObservableProperty] private string _selectedGameWorld = "Navezgane";
    [ObservableProperty] private string _gameName = "MyGame";
    [ObservableProperty] private string _worldGenSeed = "";
    [ObservableProperty] private int _worldGenSize = 6144;
    [ObservableProperty] private int _serverPort = 26900;
    [ObservableProperty] private int _telnetPort = 8081;
    [ObservableProperty] private int _webDashboardPort = 8080;
    [ObservableProperty] private int _maxPlayers = 8;
    [ObservableProperty] private string _serverPassword = "";
    [ObservableProperty] private bool _eacEnabled = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRwg))]
    private string _gameWorld = "Navezgane";

    public bool IsRwg => GameWorld.Equals("RWG", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> GameWorldOptions { get; } = new[]
    {
        "Navezgane",
        "RWG",
        "Pregen6k",
        "Pregen8k",
        "Pregen10k"
    };

    public IReadOnlyList<int> WorldGenSizeOptions { get; } = new[]
    {
        2048, 4096, 6144, 8192, 10240, 12288, 16384
    };

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void BrowseLogo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose server banner image",
            Filter = "Images|*.jpg;*.jpeg;*.png;*.webp;*.bmp|All files|*.*"
        };
        if (dialog.ShowDialog() == true)
            LogoPath = dialog.FileName;
    }

    // ── Result ────────────────────────────────────────────────────────────────

    public bool HasValidationError(out string message)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            message = "Server name is required.";
            return true;
        }
        message = "";
        return false;
    }

    public Server BuildServer()
    {
        var name       = Name?.Trim() ?? "Server";
        var root       = App.DataStore.GetAppSettings().DefaultInstallRoot;
        var safeName   = string.Concat(name.Split(Path.GetInvalidFileNameChars()));
        var installDir = Path.Combine(root, safeName);

        var server = new Server
        {
            Name = name,
            InstallDir = installDir,
            LogoPath = LogoPath?.Trim() ?? "",
            GameWorld = GameWorld,
            GameName = GameName.Trim(),
            WorldGenSeed = WorldGenSeed.Trim(),
            WorldGenSize = WorldGenSize,
            ServerPort = ServerPort,
            TelnetPort = TelnetPort,
            WebDashboardPort = WebDashboardPort,
            MaxPlayers = MaxPlayers,
            ServerPassword = ServerPassword,
            EacEnabled = EacEnabled,
            TelnetPassword = Guid.NewGuid().ToString("N")[..12]
        };
        return server;
    }
}
