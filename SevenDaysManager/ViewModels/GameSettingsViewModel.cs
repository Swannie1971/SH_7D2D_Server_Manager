using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public record LabeledValue(int Value, string Label);

/// <summary>
/// Game Settings for V3.0.
///
/// <para>Gameplay is no longer a set of XML properties — it's the single <c>SandboxCode</c>
/// string. This VM keeps the FULL decoded code and surfaces EVERY option as a dropdown,
/// grouped by <see cref="SandboxOption.Category"/> — the same grouping the game's own Sandbox
/// Settings screen uses — and re-encodes everything on save. Nothing is hidden: what you see
/// here is the full set the in-game screen shows.</para>
///
/// <para>Every dropdown's choices come from the game's own value list for that option, so the
/// UI cannot construct a code the game would reject.</para>
/// </summary>
public partial class GameSettingsViewModel : ObservableObject
{
    private readonly Server              _server;
    private readonly ServerConfigService _configService = new();

    /// <summary>Full decoded sandbox state: option id -> value index. The source of truth.</summary>
    private Dictionary<int, int> _sandbox = new();

    /// <summary>Every sandbox option as a dropdown, in display order.</summary>
    public List<SandboxOptionViewModel> Options { get; } = new();

    // Grouped for the UI — one group per SandboxOption.Category.
    public IEnumerable<SandboxOptionViewModel> GeneralOptions  => Group("General");
    public IEnumerable<SandboxOptionViewModel> EntitiesOptions => Group("Entities");
    public IEnumerable<SandboxOptionViewModel> WorldOptions    => Group("World");
    public IEnumerable<SandboxOptionViewModel> ResourcesOptions=> Group("Resources");
    public IEnumerable<SandboxOptionViewModel> CraftingOptions => Group("Crafting");
    public IEnumerable<SandboxOptionViewModel> TradersOptions  => Group("Traders");
    public IEnumerable<SandboxOptionViewModel> TasksOptions    => Group("Tasks");
    public IEnumerable<SandboxOptionViewModel> MiscOptions     => Group("Misc");

    private readonly Dictionary<string, List<SandboxOptionViewModel>> _groups = new();
    private IEnumerable<SandboxOptionViewModel> Group(string name) =>
        _groups.TryGetValue(name, out var g) ? g : Enumerable.Empty<SandboxOptionViewModel>();

    // ── Server-level settings that are STILL plain XML properties in V3.0 ──────
    [ObservableProperty] private int _playerKillingMode;
    [ObservableProperty] private int _maxSpawnedZombies;
    [ObservableProperty] private int _maxSpawnedAnimals;
    [ObservableProperty] private int _serverMaxAllowedViewDistance;
    [ObservableProperty] private int _landClaimSize;
    [ObservableProperty] private int _landClaimExpiryTime;
    [ObservableProperty] private int _landClaimOfflineDurabilityModifier;
    [ObservableProperty] private int _playerSafeZoneLevel;
    [ObservableProperty] private int _playerSafeZoneHours;

    // ── Sandbox code ──────────────────────────────────────────────────────────
    [ObservableProperty] private string _sandboxCode = "";

    /// <summary>What the user typed into the import box (never written to disk unvalidated).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanImport))]
    private string _importCode = "";

    [ObservableProperty] private string _importStatus = "";
    [ObservableProperty] private bool   _importFailed;

    public bool CanImport => !string.IsNullOrWhiteSpace(ImportCode);

    // ── Save state ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _confirmPending;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSaveError))]
    private string _saveError = "";
    [ObservableProperty] private bool _isSaved;
    public bool HasSaveError => !string.IsNullOrEmpty(SaveError);

    public string ServerName   => _server.Name;
    public string InstallDir   => _server.InstallDir;
    public bool   ConfigExists => _configService.ConfigExists(_server.InstallDir);

    /// <summary>The six stock difficulty presets, as one-click buttons.</summary>
    public IReadOnlyList<PresetButtonViewModel> DifficultyPresets { get; } =
        SandboxSettings.Presets.Where(p => p.IsDifficulty)
                               .Select(p => new PresetButtonViewModel(p)).ToList();

    /// <summary>The themed presets (Caveman Life, Dying World…).</summary>
    public IReadOnlyList<PresetButtonViewModel> ThemePresets { get; } =
        SandboxSettings.Presets.Where(p => !p.IsDifficulty)
                               .Select(p => new PresetButtonViewModel(p)).ToList();

    private IEnumerable<PresetButtonViewModel> AllPresets =>
        DifficultyPresets.Concat(ThemePresets);

    // ── Which preset are we on? ───────────────────────────────────────────────

    /// <summary>Name of the preset the current settings exactly match, or null if none do.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCustom))]
    [NotifyPropertyChangedFor(nameof(ProfileLabel))]
    private string? _activePreset;

    /// <summary>True when the settings don't match any stock preset — i.e. a custom profile.</summary>
    public bool IsCustom => ActivePreset is null;

    /// <summary>What the badge reads: "SCAVENGER" or "CUSTOM".</summary>
    public string ProfileLabel => ActivePreset?.ToUpperInvariant() ?? "CUSTOM";

    /// <summary>
    /// Work out which preset (if any) the current state is, and light up its button.
    ///
    /// The comparison is over the FULL decoded state, not just the difficulty fields: change
    /// the air-drop rate on Scavenger and it is no longer Scavenger, even though the damage
    /// multipliers still match. Anything that isn't an exact match is CUSTOM.
    /// </summary>
    private void RefreshActivePreset()
    {
        string? match = null;

        foreach (var vm in AllPresets)
        {
            var theirs = SandboxCodeService.Decode(vm.Preset.Code);
            var same   = _sandbox.Count == theirs.Count &&
                         _sandbox.All(kv => theirs.TryGetValue(kv.Key, out var v) && v == kv.Value);

            vm.IsActive = same;
            if (same) match = vm.Preset.Name;
        }

        ActivePreset = match;
    }

    // ── Still-valid XML option lists ──────────────────────────────────────────

    public IReadOnlyList<LabeledValue> PlayerKillingOptions { get; } = new[]
    {
        new LabeledValue(0, "No Killing — PvE only"),
        new LabeledValue(1, "Kill Allies Only"),
        new LabeledValue(2, "Kill Strangers Only"),
        new LabeledValue(3, "Always — full PvP"),
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

    public IReadOnlyList<LabeledValue> MaxZombieOptions { get; } = new[]
    {
        new LabeledValue(16, "16 — low-end host"),
        new LabeledValue(32, "32"),
        new LabeledValue(48, "48"),
        new LabeledValue(64, "64 — default"),
        new LabeledValue(80, "80"),
        new LabeledValue(100, "100 — heavy"),
    };

    public IReadOnlyList<LabeledValue> MaxAnimalOptions { get; } = new[]
    {
        new LabeledValue(10, "10"),
        new LabeledValue(25, "25"),
        new LabeledValue(50, "50 — default"),
        new LabeledValue(75, "75"),
        new LabeledValue(100, "100"),
    };

    public GameSettingsViewModel(Server server)
    {
        _server = server;
        Load();
    }

    private void Load()
    {
        // Prefer what's actually on disk — the file is the truth; the DB is a cache.
        var code = _server.SandboxCode;
        if (string.IsNullOrWhiteSpace(code) && _configService.ConfigExists(_server.InstallDir))
            code = _configService.ReadConfig(_server.InstallDir).SandboxCode;

        _sandbox = SandboxCodeService.Decode(code);
        BuildOptions();
        RefreshCode();

        PlayerKillingMode   = _server.PlayerKillingMode;
        MaxSpawnedZombies   = _server.MaxSpawnedZombies;
        MaxSpawnedAnimals   = _server.MaxSpawnedAnimals;
        ServerMaxAllowedViewDistance = _server.ServerMaxAllowedViewDistance;
        LandClaimSize       = _server.LandClaimSize;
        LandClaimExpiryTime = _server.LandClaimExpiryTime;
        LandClaimOfflineDurabilityModifier = _server.LandClaimOfflineDurabilityModifier;
        PlayerSafeZoneLevel = _server.PlayerSafeZoneLevel;
        PlayerSafeZoneHours = _server.PlayerSafeZoneHours;
    }

    private void BuildOptions()
    {
        Options.Clear();
        _groups.Clear();

        foreach (var opt in SandboxSettings.All)
        {
            var idx = _sandbox.TryGetValue(opt.Id, out var v) ? v : opt.DefaultIndex;
            var vm  = new SandboxOptionViewModel(opt, idx);

            // Any change re-encodes, so the code preview always matches the dropdowns.
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(SandboxOptionViewModel.Selected)) return;
                _sandbox[vm.Option.Id] = vm.SelectedIndex;
                RefreshCode();
            };

            Options.Add(vm);
            if (!_groups.TryGetValue(opt.Category, out var list))
                _groups[opt.Category] = list = new List<SandboxOptionViewModel>();
            list.Add(vm);
        }

        OnPropertyChanged(nameof(GeneralOptions));
        OnPropertyChanged(nameof(EntitiesOptions));
        OnPropertyChanged(nameof(WorldOptions));
        OnPropertyChanged(nameof(ResourcesOptions));
        OnPropertyChanged(nameof(CraftingOptions));
        OnPropertyChanged(nameof(TradersOptions));
        OnPropertyChanged(nameof(TasksOptions));
        OnPropertyChanged(nameof(MiscOptions));
    }

    /// <summary>
    /// Re-encode the full sandbox state (not just the options we show), and re-evaluate which
    /// preset — if any — it now matches. Every path that mutates _sandbox calls this, so the
    /// code preview and the CUSTOM badge can never drift from the actual settings.
    /// </summary>
    private void RefreshCode()
    {
        SandboxCode = SandboxCodeService.Encode(_sandbox);
        RefreshActivePreset();
    }

    /// <summary>
    /// Apply a stock preset — this is what "difficulty" now means in V3.0.
    ///
    /// A preset is a STARTING POINT, not a mode. It replaces the whole state, but nothing
    /// stops the user then tweaking individual options; the badge just flips to CUSTOM.
    /// </summary>
    [RelayCommand]
    private void ApplyPreset(PresetButtonViewModel? button)
    {
        if (button is null) return;

        // Replaces the whole sandbox state, exactly as picking it in-game would.
        _sandbox = SandboxCodeService.Decode(button.Preset.Code);
        BuildOptions();
        RefreshCode();

        ImportStatus = $"Applied preset: {button.Preset.Name}";
        ImportFailed = false;
        IsSaved      = false;
    }

    /// <summary>
    /// Import a code from the game client. It is DECODED into the dropdowns — never written
    /// through to the config as-is. If it doesn't parse, nothing changes.
    /// </summary>
    [RelayCommand]
    private void Import()
    {
        var code = (ImportCode ?? "").Trim();

        if (!SandboxCodeService.IsValid(code))
        {
            ImportStatus = "That isn't a valid sandbox code. Copy it from the game's " +
                           "Sandbox Options screen using the Copy Code button.";
            ImportFailed = true;
            return;
        }

        _sandbox = SandboxCodeService.Decode(code);
        BuildOptions();
        RefreshCode();

        var shown = _sandbox.Count;
        ImportStatus = $"Imported — {shown} option{(shown == 1 ? "" : "s")} set. " +
                       "Review below, then Save.";
        ImportFailed = false;
        ImportCode   = "";
        IsSaved      = false;
    }

    /// <summary>Copy the current code, so it can be pasted into the game or another panel.</summary>
    [RelayCommand]
    private void CopyCode()
    {
        if (string.IsNullOrWhiteSpace(SandboxCode)) return;
        try
        {
            System.Windows.Clipboard.SetText(SandboxCode);
            ImportStatus = "Sandbox code copied to the clipboard.";
            ImportFailed = false;
        }
        catch (Exception ex)
        {
            // The clipboard can be locked by another process; not worth failing the whole tab.
            ImportStatus = $"Could not copy: {ex.Message}";
            ImportFailed = true;
        }
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
            _server.SandboxCode = SandboxCode;

            _server.PlayerKillingMode   = PlayerKillingMode;
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
