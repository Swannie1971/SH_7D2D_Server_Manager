using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public record LabeledValue(int Value, string Label);

public partial class GameSettingsViewModel : ObservableObject
{
    private readonly Server             _server;
    private readonly ServerConfigService _configService = new();

    // ── Difficulty & Progression ──────────────────────────────────────────────
    [ObservableProperty] private int _gameDifficulty;
    [ObservableProperty] private int _xpMultiplier;
    [ObservableProperty] private int _playerKillingMode;

    // ── Time ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private int _dayNightLength;
    [ObservableProperty] private int _dayLightLength;

    // ── Zombies ───────────────────────────────────────────────────────────────
    [ObservableProperty] private int _zombieMove;
    [ObservableProperty] private int _zombieMoveNight;
    [ObservableProperty] private int _zombieFeralMove;

    // ── Blood Moon ────────────────────────────────────────────────────────────
    [ObservableProperty] private int _bloodMoonFrequency;
    [ObservableProperty] private int _bloodMoonEnemyCount;

    // ── Loot & Drops ─────────────────────────────────────────────────────────
    [ObservableProperty] private int _lootAbundance;
    [ObservableProperty] private int _lootRespawnDays;
    [ObservableProperty] private int _airDropFrequency;
    [ObservableProperty] private int _dropOnDeath;
    [ObservableProperty] private int _dropOnQuit;

    // ── Zombies (extra) ───────────────────────────────────────────────────────
    [ObservableProperty] private int _zombieBMMove;
    [ObservableProperty] private int _maxSpawnedZombies;
    [ObservableProperty] private int _maxSpawnedAnimals;

    // ── Blood Moon (extra) ────────────────────────────────────────────────────
    [ObservableProperty] private int _bloodMoonRange;

    // ── Land Claims ───────────────────────────────────────────────────────────
    [ObservableProperty] private int _landClaimSize;
    [ObservableProperty] private int _landClaimExpiryTime;
    [ObservableProperty] private int _landClaimOfflineDurabilityModifier;

    // ── New Player Protection ─────────────────────────────────────────────────
    [ObservableProperty] private int _playerSafeZoneLevel;
    [ObservableProperty] private int _playerSafeZoneHours;

    // ── Performance ───────────────────────────────────────────────────────────
    [ObservableProperty] private int _serverMaxAllowedViewDistance;

    // ── Save state ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _confirmPending;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveError))]
    private string _saveError = "";
    [ObservableProperty] private bool _isSaved;
    public bool HasSaveError => !string.IsNullOrEmpty(SaveError);

    public string ServerName  => _server.Name;
    public string InstallDir  => _server.InstallDir;
    public bool   ConfigExists => _configService.ConfigExists(_server.InstallDir);

    // ── Options lists ─────────────────────────────────────────────────────────

    public IReadOnlyList<LabeledValue> DifficultyOptions { get; } = new[]
    {
        new LabeledValue(0, "Scavenger — very easy"),
        new LabeledValue(1, "Adventurer — easy"),
        new LabeledValue(2, "Warrior — medium (default)"),
        new LabeledValue(3, "Survivalist — hard"),
        new LabeledValue(4, "Nomad — very hard"),
        new LabeledValue(5, "Insane — extreme"),
    };

    public IReadOnlyList<LabeledValue> XpOptions { get; } = new[]
    {
        new LabeledValue(25,  "25% — very slow"),
        new LabeledValue(50,  "50%"),
        new LabeledValue(75,  "75%"),
        new LabeledValue(100, "100% — default"),
        new LabeledValue(150, "150%"),
        new LabeledValue(200, "200%"),
        new LabeledValue(300, "300% — very fast"),
    };

    public IReadOnlyList<LabeledValue> PlayerKillingOptions { get; } = new[]
    {
        new LabeledValue(0, "No Killing — PvE only"),
        new LabeledValue(1, "Kill Allies Only"),
        new LabeledValue(2, "Kill Strangers Only"),
        new LabeledValue(3, "Always — full PvP"),
    };

    public IReadOnlyList<LabeledValue> DayLengthOptions { get; } = new[]
    {
        new LabeledValue(10,  "10 min — very short"),
        new LabeledValue(20,  "20 min"),
        new LabeledValue(30,  "30 min"),
        new LabeledValue(40,  "40 min"),
        new LabeledValue(50,  "50 min"),
        new LabeledValue(60,  "60 min — default"),
        new LabeledValue(80,  "80 min"),
        new LabeledValue(100, "100 min"),
        new LabeledValue(120, "120 min — long"),
    };

    public IReadOnlyList<LabeledValue> DayLightOptions { get; } =
        Enumerable.Range(6, 19).Select(h => new LabeledValue(h, h == 18 ? $"{h}h — default" : $"{h}h")).ToList();

    public IReadOnlyList<LabeledValue> ZombieSpeedOptions { get; } = new[]
    {
        new LabeledValue(0, "Walk"),
        new LabeledValue(1, "Jog"),
        new LabeledValue(2, "Run"),
        new LabeledValue(3, "Sprint"),
        new LabeledValue(4, "Nightmare"),
    };

    public IReadOnlyList<LabeledValue> LootAbundanceOptions { get; } = new[]
    {
        new LabeledValue(25,  "25% — scarce"),
        new LabeledValue(50,  "50%"),
        new LabeledValue(75,  "75%"),
        new LabeledValue(100, "100% — default"),
        new LabeledValue(150, "150%"),
        new LabeledValue(200, "200% — plenty"),
    };

    public IReadOnlyList<LabeledValue> LootRespawnOptions { get; } = new[]
    {
        new LabeledValue(0,  "Never"),
        new LabeledValue(3,  "3 days"),
        new LabeledValue(5,  "5 days"),
        new LabeledValue(7,  "7 days — default"),
        new LabeledValue(10, "10 days"),
        new LabeledValue(14, "14 days"),
        new LabeledValue(30, "30 days"),
    };

    public IReadOnlyList<LabeledValue> AirDropOptions { get; } = new[]
    {
        new LabeledValue(0,  "Never"),
        new LabeledValue(24, "Every day"),
        new LabeledValue(48, "Every 2 days"),
        new LabeledValue(72, "Every 3 days — default"),
    };

    public IReadOnlyList<LabeledValue> DropOnDeathOptions { get; } = new[]
    {
        new LabeledValue(0, "Nothing"),
        new LabeledValue(1, "Everything — default"),
        new LabeledValue(2, "Toolbelt only"),
        new LabeledValue(3, "Backpack only"),
        new LabeledValue(4, "Delete all"),
    };

    public IReadOnlyList<LabeledValue> DropOnQuitOptions { get; } = new[]
    {
        new LabeledValue(0, "Nothing — default"),
        new LabeledValue(1, "Everything"),
        new LabeledValue(2, "Toolbelt only"),
        new LabeledValue(3, "Backpack only"),
    };

    public IReadOnlyList<LabeledValue> LandClaimSizeOptions { get; } = new[]
    {
        new LabeledValue(21, "21 blocks — small"),
        new LabeledValue(31, "31 blocks"),
        new LabeledValue(41, "41 blocks — default"),
        new LabeledValue(51, "51 blocks"),
        new LabeledValue(61, "61 blocks"),
        new LabeledValue(71, "71 blocks — large"),
    };

    public IReadOnlyList<LabeledValue> LandClaimExpiryOptions { get; } = new[]
    {
        new LabeledValue(0,  "Never expires"),
        new LabeledValue(1,  "1 day"),
        new LabeledValue(3,  "3 days"),
        new LabeledValue(7,  "7 days — default"),
        new LabeledValue(14, "14 days"),
        new LabeledValue(30, "30 days"),
    };

    public IReadOnlyList<LabeledValue> LandClaimOfflineOptions { get; } = new[]
    {
        new LabeledValue(0, "Indestructible — default"),
        new LabeledValue(1, "Normal durability"),
        new LabeledValue(2, "2× easier to raid"),
        new LabeledValue(4, "4× easier to raid"),
    };

    public IReadOnlyList<LabeledValue> SafeZoneLevelOptions { get; } = new[]
    {
        new LabeledValue(0,  "Off — no protection"),
        new LabeledValue(1,  "Level 1"),
        new LabeledValue(2,  "Level 2"),
        new LabeledValue(3,  "Level 3"),
        new LabeledValue(5,  "Level 5 — default"),
        new LabeledValue(10, "Level 10"),
    };

    public IReadOnlyList<LabeledValue> SafeZoneHoursOptions { get; } = new[]
    {
        new LabeledValue(0,  "Off — no protection"),
        new LabeledValue(1,  "1 hour"),
        new LabeledValue(2,  "2 hours"),
        new LabeledValue(5,  "5 hours — default"),
        new LabeledValue(10, "10 hours"),
        new LabeledValue(24, "24 hours"),
    };

    public IReadOnlyList<LabeledValue> ViewDistanceOptions { get; } = new[]
    {
        new LabeledValue(4,  "4 chunks — lowest"),
        new LabeledValue(6,  "6 chunks"),
        new LabeledValue(8,  "8 chunks"),
        new LabeledValue(10, "10 chunks"),
        new LabeledValue(12, "12 chunks — default"),
        new LabeledValue(14, "14 chunks"),
        new LabeledValue(16, "16 chunks — highest"),
    };

    public GameSettingsViewModel(Server server)
    {
        _server = server;
        Load();
    }

    private void Load()
    {
        GameDifficulty      = _server.GameDifficulty;
        XpMultiplier        = _server.XPMultiplier;
        PlayerKillingMode   = _server.PlayerKillingMode;
        DayNightLength      = _server.DayNightLength;
        DayLightLength      = _server.DayLightLength;
        ZombieMove          = _server.ZombieMove;
        ZombieMoveNight     = _server.ZombieMoveNight;
        ZombieFeralMove     = _server.ZombieFeralMove;
        BloodMoonFrequency  = _server.BloodMoonFrequency;
        BloodMoonEnemyCount = _server.BloodMoonEnemyCount;
        LootAbundance       = _server.LootAbundance;
        LootRespawnDays     = _server.LootRespawnDays;
        AirDropFrequency    = _server.AirDropFrequency;
        DropOnDeath         = _server.DropOnDeath;
        DropOnQuit          = _server.DropOnQuit;
        ZombieBMMove        = _server.ZombieBMMove;
        BloodMoonRange      = _server.BloodMoonRange;
        MaxSpawnedZombies   = _server.MaxSpawnedZombies;
        MaxSpawnedAnimals   = _server.MaxSpawnedAnimals;
        ServerMaxAllowedViewDistance = _server.ServerMaxAllowedViewDistance;
        LandClaimSize       = _server.LandClaimSize;
        LandClaimExpiryTime = _server.LandClaimExpiryTime;
        LandClaimOfflineDurabilityModifier = _server.LandClaimOfflineDurabilityModifier;
        PlayerSafeZoneLevel = _server.PlayerSafeZoneLevel;
        PlayerSafeZoneHours = _server.PlayerSafeZoneHours;
    }

    [RelayCommand]
    private void RequestSave() => ConfirmPending = true;

    [RelayCommand]
    private void CancelSave() => ConfirmPending = false;

    [RelayCommand]
    private void ConfirmSave()
    {
        ConfirmPending = false;
        try
        {
            _server.GameDifficulty      = GameDifficulty;
            _server.XPMultiplier        = XpMultiplier;
            _server.PlayerKillingMode   = PlayerKillingMode;
            _server.DayNightLength      = DayNightLength;
            _server.DayLightLength      = DayLightLength;
            _server.ZombieMove          = ZombieMove;
            _server.ZombieMoveNight     = ZombieMoveNight;
            _server.ZombieFeralMove     = ZombieFeralMove;
            _server.BloodMoonFrequency  = BloodMoonFrequency;
            _server.BloodMoonEnemyCount = BloodMoonEnemyCount;
            _server.LootAbundance       = LootAbundance;
            _server.LootRespawnDays     = LootRespawnDays;
            _server.AirDropFrequency    = AirDropFrequency;
            _server.DropOnDeath         = DropOnDeath;
            _server.DropOnQuit          = DropOnQuit;
            _server.ZombieBMMove        = ZombieBMMove;
            _server.BloodMoonRange      = BloodMoonRange;
            _server.MaxSpawnedZombies   = MaxSpawnedZombies;
            _server.MaxSpawnedAnimals   = MaxSpawnedAnimals;
            _server.ServerMaxAllowedViewDistance = ServerMaxAllowedViewDistance;
            _server.LandClaimSize       = LandClaimSize;
            _server.LandClaimExpiryTime = LandClaimExpiryTime;
            _server.LandClaimOfflineDurabilityModifier = LandClaimOfflineDurabilityModifier;
            _server.PlayerSafeZoneLevel = PlayerSafeZoneLevel;
            _server.PlayerSafeZoneHours = PlayerSafeZoneHours;

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
}
