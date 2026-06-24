using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class ConfigViewModel : ObservableObject
{
    private readonly Server _server;
    private readonly ServerConfigService _configService = new();

    // ── Server Identity ───────────────────────────────────────────────────────
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private string _description = "";
    [ObservableProperty] private string _websiteUrl = "";
    [ObservableProperty] private string _serverPassword = "";
    [ObservableProperty] private int _serverVisibility;
    [ObservableProperty] private int _maxPlayers;

    // ── World ─────────────────────────────────────────────────────────────────
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRwg))]
    private string _gameWorld = "Navezgane";
    [ObservableProperty] private string _gameName = "";
    [ObservableProperty] private string _worldGenSeed = "";
    [ObservableProperty] private int _worldGenSize;

    // ── Network ───────────────────────────────────────────────────────────────
    [ObservableProperty] private int _serverPort;
    [ObservableProperty] private int _telnetPort;
    [ObservableProperty] private int _webDashboardPort;

    // ── Options ───────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _eacEnabled;

    public bool IsRwg => GameWorld.Equals("RWG", StringComparison.OrdinalIgnoreCase);

    public IReadOnlyList<string> GameWorldOptions { get; }

    private static IReadOnlyList<string> LoadWorldOptions(string installDir)
    {
        // RWG always available; Navezgane always included; Pregen worlds are optional downloads
        var worlds = new List<string> { "Navezgane", "RWG" };

        var worldsPath = Path.Combine(installDir, "Data", "Worlds");
        if (Directory.Exists(worldsPath))
        {
            var found = Directory.GetDirectories(worldsPath)
                .Select(Path.GetFileName)
                .Where(n => !string.IsNullOrEmpty(n) && !worlds.Contains(n))
                .OrderBy(n => n)
                .ToList();
            worlds.AddRange(found!);
        }

        return worlds;
    }

    public IReadOnlyList<int> WorldGenSizeOptions { get; } =
        new[] { 2048, 4096, 6144, 8192, 10240, 12288, 16384 };

    public IReadOnlyList<VisibilityOption> VisibilityOptions { get; } = new[]
    {
        new VisibilityOption(0, "Private"),
        new VisibilityOption(1, "Friends Only"),
        new VisibilityOption(2, "Public"),
    };

    public string ServerName => _server.Name;
    public string InstallDir  => _server.InstallDir;
    public bool   ConfigExists => _configService.ConfigExists(_server.InstallDir);

    public ConfigViewModel(Server server)
    {
        _server = server;
        GameWorldOptions = LoadWorldOptions(server.InstallDir);
        Load();
    }

    private void Load()
    {
        Name            = _server.Name;
        Description     = _server.Description;
        WebsiteUrl      = _server.WebsiteUrl;
        ServerPassword  = _server.ServerPassword;
        ServerVisibility = _server.Visibility;
        MaxPlayers      = _server.MaxPlayers;
        GameWorld       = _server.GameWorld;
        GameName        = _server.GameName;
        WorldGenSeed    = _server.WorldGenSeed;
        WorldGenSize    = _server.WorldGenSize;
        ServerPort      = _server.ServerPort;
        TelnetPort      = _server.TelnetPort;
        WebDashboardPort = _server.WebDashboardPort;
        EacEnabled      = _server.EacEnabled;
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            _server.Name             = Name?.Trim() ?? "";
            _server.Description      = Description?.Trim() ?? "";
            _server.WebsiteUrl       = WebsiteUrl?.Trim() ?? "";
            _server.ServerPassword   = ServerPassword ?? "";
            _server.Visibility       = ServerVisibility;
            _server.MaxPlayers       = MaxPlayers;
            _server.GameWorld        = GameWorld ?? "Navezgane";
            _server.GameName         = GameName?.Trim() ?? "";
            _server.WorldGenSeed     = WorldGenSeed?.Trim() ?? "";
            _server.WorldGenSize     = WorldGenSize;
            _server.ServerPort       = ServerPort;
            _server.TelnetPort       = TelnetPort;
            _server.WebDashboardPort = WebDashboardPort;
            _server.EacEnabled       = EacEnabled;

            App.DataStore.SaveServer(_server);

            if (Directory.Exists(_server.InstallDir))
                _configService.WriteConfig(_server);

            SaveError = "";
            IsSaved   = true;
        }
        catch (Exception ex)
        {
            SaveError = $"Save failed: {ex.Message}";
            IsSaved   = false;
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveError))]
    private string _saveError = "";

    public bool HasSaveError => !string.IsNullOrEmpty(SaveError);

    [ObservableProperty] private bool _isSaved;
}

public record VisibilityOption(int Value, string Label);
