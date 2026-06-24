using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class ColorSwatchViewModel : ObservableObject
{
    public string Name { get; }
    public Color Color { get; }
    public SolidColorBrush Brush { get; }

    [ObservableProperty]
    private bool _isSelectedPrimary;

    [ObservableProperty]
    private bool _isSelectedSecondary;

    public ColorSwatchViewModel(SwatchInfo info)
    {
        Name = info.Name;
        Color = info.Color;
        Brush = new SolidColorBrush(info.Color);
    }
}

public partial class AppSettingsViewModel : ObservableObject
{
    private readonly AppThemeSettings _settings;
    private readonly Models.AppSettings _appSettings;

    public ObservableCollection<ColorSwatchViewModel> Swatches { get; } = new();

    [ObservableProperty] private bool _isDark;
    [ObservableProperty] private ColorSwatchViewModel? _selectedPrimary;
    [ObservableProperty] private ColorSwatchViewModel? _selectedSecondary;

    // ── Paths ─────────────────────────────────────────────────────────────────
    [ObservableProperty] private string _defaultInstallRoot = "";

    public string SteamCmdExePreview => System.IO.Path.Combine(
        string.IsNullOrWhiteSpace(DefaultInstallRoot) ? "…" : DefaultInstallRoot,
        "steamcmd", "steamcmd.exe");

    public AppSettingsViewModel()
    {
        _settings    = ThemeService.Load();
        _appSettings = App.DataStore.GetAppSettings();
        _isDark      = _settings.IsDark;
        _defaultInstallRoot = _appSettings.DefaultInstallRoot;

        foreach (var info in ThemeService.GetSwatches())
            Swatches.Add(new ColorSwatchViewModel(info));

        _selectedPrimary = Swatches.FirstOrDefault(s =>
            s.Name.Equals(_settings.PrimaryColor, StringComparison.OrdinalIgnoreCase));
        _selectedSecondary = Swatches.FirstOrDefault(s =>
            s.Name.Equals(_settings.SecondaryColor, StringComparison.OrdinalIgnoreCase));

        UpdateSelectionMarkers();
    }

    [RelayCommand]
    private void SelectPrimary(ColorSwatchViewModel swatch)
    {
        SelectedPrimary = swatch;
        UpdateSelectionMarkers();
        ApplyTheme();
    }

    [RelayCommand]
    private void SelectSecondary(ColorSwatchViewModel swatch)
    {
        SelectedSecondary = swatch;
        UpdateSelectionMarkers();
        ApplyTheme();
    }

    [RelayCommand]
    private void BrowseInstallRoot()
    {
        var dlg = new Microsoft.Win32.OpenFolderDialog { Title = "Default server install root" };
        if (dlg.ShowDialog() == true) DefaultInstallRoot = dlg.FolderName;
    }

    partial void OnDefaultInstallRootChanged(string value) =>
        OnPropertyChanged(nameof(SteamCmdExePreview));

    public void SavePaths()
    {
        _appSettings.DefaultInstallRoot = DefaultInstallRoot?.Trim() ?? "";
        App.DataStore.SaveAppSettings(_appSettings);
    }

    partial void OnIsDarkChanged(bool value) => ApplyTheme();

    private void ApplyTheme()
    {
        _settings.IsDark = IsDark;
        _settings.PrimaryColor = SelectedPrimary?.Name ?? "Teal";
        _settings.SecondaryColor = SelectedSecondary?.Name ?? "LightGreen";
        ThemeService.Apply(_settings);
    }

    private void UpdateSelectionMarkers()
    {
        foreach (var s in Swatches)
        {
            s.IsSelectedPrimary = s == SelectedPrimary;
            s.IsSelectedSecondary = s == SelectedSecondary;
        }
    }
}
