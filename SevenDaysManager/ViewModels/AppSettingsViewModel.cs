using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

using SevenDaysManager.Views;

namespace SevenDaysManager.ViewModels;

public partial class AppSettingsViewModel : ObservableObject
{
    private readonly AppThemeSettings _settings;
    private readonly Models.AppSettings _appSettings;

    // ── Paths ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _defaultInstallRoot = "";

    // ── Behaviour ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _autoStartOnLogin;

    // ── Panel appearance ─────────────────────────────────────────────────────
    // The tactical HUD palette is fixed, so the only appearance knob left is how
    // translucent panels are over the background image.
    [ObservableProperty] private int _cardOpacity;

    // ── Display font ─────────────────────────────────────────────────────────
    // Headers and titles only. Data stays mono — see FontService.
    public IReadOnlyList<FontService.FontOption> FontOptions => FontService.Options;

    [ObservableProperty] private FontService.FontOption _displayFont = FontService.Options[0];

    // ── Background image ─────────────────────────────────────────────────────
    [ObservableProperty] private string _backgroundImagePath = "";
    public bool HasCustomBackground => !string.IsNullOrWhiteSpace(BackgroundImagePath);

    // True when not running as a published exe — shows an explanatory note in the UI
    public bool IsDevBuild => AutoStartService.GetExePath() is null;

    public string SteamCmdExePreview => System.IO.Path.Combine(
        string.IsNullOrWhiteSpace(DefaultInstallRoot) ? "…" : DefaultInstallRoot,
        "steamcmd", "steamcmd.exe");

    public AppSettingsViewModel()
    {
        _settings    = ThemeService.Load();
        _appSettings = App.DataStore.GetAppSettings();

        _defaultInstallRoot  = _appSettings.DefaultInstallRoot;
        _startMinimized      = _appSettings.StartMinimized;
        _autoStartOnLogin    = AutoStartService.IsEnabled();
        _cardOpacity         = _appSettings.CardOpacity;
        _backgroundImagePath = _appSettings.BackgroundImagePath;

        // Match on the stored family name; fall back to the default if it's unknown
        // (e.g. an older document, or a font we've since dropped from the list).
        _displayFont = FontService.Options.FirstOrDefault(o =>
                           string.Equals(o.Family, _appSettings.DisplayFont,
                                         StringComparison.OrdinalIgnoreCase))
                       ?? FontService.Options[0];
    }

    [RelayCommand]
    private void BrowseInstallRoot()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Default server install root" };
        if (dlg.ShowDialog() == true) DefaultInstallRoot = dlg.FolderName;
    }

    partial void OnDefaultInstallRootChanged(string value) =>
        OnPropertyChanged(nameof(SteamCmdExePreview));

    partial void OnAutoStartOnLoginChanged(bool value)
    {
        if (value)
        {
            if (!AutoStartService.Enable())
            {
                // Silently revert — toggle will snap back
                _autoStartOnLogin = false;
                OnPropertyChanged(nameof(AutoStartOnLogin));
                HudDialog.Show(
                    "Auto-start could not be registered.\n\nThis only works with a published build, not when running via 'dotnet run'.",
                    "Auto-start", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            AutoStartService.Disable();
        }
    }

    partial void OnCardOpacityChanged(int value) => CardBrushService.Apply(value);

    // Live preview: swapping the resource restyles every header immediately, no restart.
    partial void OnDisplayFontChanged(FontService.FontOption value) =>
        FontService.Apply(value.Family);

    [RelayCommand]
    private void BrowseBackgroundImage()
    {
        var dlg = new OpenFileDialog
        {
            Title  = "Choose a background image",
            Filter = "Image files (*.jpg;*.jpeg;*.png;*.bmp)|*.jpg;*.jpeg;*.png;*.bmp|All files (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        BackgroundImagePath = dlg.FileName;
        BackgroundImageService.Apply(BackgroundImagePath);
    }

    [RelayCommand]
    private void ResetBackgroundImage()
    {
        BackgroundImagePath = "";
        BackgroundImageService.Apply(BackgroundImagePath);
    }

    partial void OnBackgroundImagePathChanged(string value) =>
        OnPropertyChanged(nameof(HasCustomBackground));

    public void Save()
    {
        _appSettings.DefaultInstallRoot   = DefaultInstallRoot?.Trim() ?? "";
        _appSettings.StartMinimized       = StartMinimized;
        _appSettings.CardOpacity          = CardOpacity;
        _appSettings.BackgroundImagePath  = BackgroundImagePath?.Trim() ?? "";
        _appSettings.DisplayFont          = DisplayFont.Family;
        App.DataStore.SaveAppSettings(_appSettings);
        ThemeService.Apply(_settings);
    }
}
