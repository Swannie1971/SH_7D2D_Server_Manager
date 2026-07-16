using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class AddServerViewModel : ObservableObject
{
    // ── Form fields ───────────────────────────────────────────────────────────

    [ObservableProperty] private string _name = "My Server";
    [ObservableProperty] private string _installDir = "";
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

    // Tracks whether the user has taken the install path into their own hands (typed in it,
    // or used Browse). Until then, InstallDir auto-follows the server Name so the common case
    // needs zero clicks — but once they've touched it, we must never overwrite their choice.
    private bool _installDirManuallySet;

    // Set around every PROGRAMMATIC write to InstallDir, so OnInstallDirChanged can tell that
    // apart from the user actually typing — without it, ComputeDefaultInstallDir's own
    // assignment below would immediately (and wrongly) flip _installDirManuallySet to true,
    // permanently breaking the auto-follow-Name behaviour after the very first character typed.
    private bool _settingInstallDirProgrammatically;

    public AddServerViewModel()
    {
        SetInstallDirProgrammatically(ComputeDefaultInstallDir(Name));
    }

    partial void OnNameChanged(string value)
    {
        if (!_installDirManuallySet)
            SetInstallDirProgrammatically(ComputeDefaultInstallDir(value));
    }

    partial void OnInstallDirChanged(string value)
    {
        if (!_settingInstallDirProgrammatically)
            _installDirManuallySet = true;
    }

    private void SetInstallDirProgrammatically(string path)
    {
        _settingInstallDirProgrammatically = true;
        try { InstallDir = path; }
        finally { _settingInstallDirProgrammatically = false; }
    }

    private static string ComputeDefaultInstallDir(string name)
    {
        var root     = App.DataStore.GetAppSettings().DefaultInstallRoot;
        var safeName = string.Concat((name ?? "Server").Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrWhiteSpace(safeName)) safeName = "Server";
        return Path.Combine(root, safeName);
    }

    // ── Commands ──────────────────────────────────────────────────────────────

    [RelayCommand]
    private void BrowseInstallDir()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Choose where this server's files will be installed"
        };
        if (!string.IsNullOrWhiteSpace(InstallDir))
            dialog.InitialDirectory = Directory.Exists(InstallDir)
                ? InstallDir
                : Path.GetDirectoryName(InstallDir) ?? "";

        if (dialog.ShowDialog() != true) return;

        _installDirManuallySet = true;
        InstallDir = dialog.FolderName;
    }

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
        if (string.IsNullOrWhiteSpace(InstallDir))
        {
            message = "Install location is required.";
            return true;
        }
        if (!IsValidRootedPath(InstallDir))
        {
            message = "Install location must be a full path (e.g. D:\\Servers\\MyServer).";
            return true;
        }
        if (CollidesWithSteamCmdFolder(InstallDir))
        {
            message = $"\"{SteamCmdService.ReservedFolderName}\" is reserved for the shared SteamCMD "
                     + "install and can't be used as a server's location. Pick a different folder.";
            return true;
        }
        message = "";
        return false;
    }

    private static bool IsValidRootedPath(string path)
    {
        try { return Path.IsPathRooted(path) && Path.GetFullPath(path) is not null; }
        catch { return false; }
    }

    // SteamCmdService relies on its reserved sibling folder NEVER being a real server's install
    // dir (or a parent/child of one) — see SteamCmdService.ReservedFolderName. Equal OR nested
    // either way both count: a server folder living inside _SteamCMD, or _SteamCMD ending up
    // inside a server folder, would both let SteamCMD's library registration collide with the
    // game install the exact way that broke installs before this was a fixed, reserved name.
    private static bool CollidesWithSteamCmdFolder(string installDir)
    {
        string a, b;
        try
        {
            a = Path.GetFullPath(installDir).TrimEnd('\\');
            b = Path.GetFullPath(SteamCmdService.SharedSteamCmdDir).TrimEnd('\\');
        }
        catch { return false; }

        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase)
            || a.StartsWith(b + "\\", StringComparison.OrdinalIgnoreCase)
            || b.StartsWith(a + "\\", StringComparison.OrdinalIgnoreCase);
    }

    public Server BuildServer()
    {
        var name = Name?.Trim() ?? "Server";

        var server = new Server
        {
            Name = name,
            // The user's explicit choice — no longer recomputed from the global default root.
            InstallDir = InstallDir.Trim(),
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
