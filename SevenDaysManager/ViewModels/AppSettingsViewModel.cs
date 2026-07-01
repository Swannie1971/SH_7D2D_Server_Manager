using System.Collections.ObjectModel;
using System.Windows;
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

    // ── Behaviour ─────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _autoStartOnLogin;

    // ── Card appearance ───────────────────────────────────────────────────────
    public ObservableCollection<CardColorSwatchVm> CardSwatches { get; } = new();
    [ObservableProperty] private CardColorSwatchVm? _selectedCardColor;
    [ObservableProperty] private int _cardOpacity;

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
        _isDark             = _settings.IsDark;
        _defaultInstallRoot = _appSettings.DefaultInstallRoot;
        _startMinimized     = _appSettings.StartMinimized;
        _autoStartOnLogin   = AutoStartService.IsEnabled();
        _cardOpacity = _appSettings.CardOpacity;
        _backgroundImagePath = _appSettings.BackgroundImagePath;
        foreach (var s in CardBrushService.Swatches)
            CardSwatches.Add(new CardColorSwatchVm(s));
        _selectedCardColor = CardSwatches
            .FirstOrDefault(s => s.Hex.Equals(_appSettings.CardColor, StringComparison.OrdinalIgnoreCase))
            ?? CardSwatches[0];
        UpdateCardSelectionMarkers();

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

    partial void OnAutoStartOnLoginChanged(bool value)
    {
        if (value)
        {
            if (!AutoStartService.Enable())
            {
                // Silently revert — toggle will snap back
                _autoStartOnLogin = false;
                OnPropertyChanged(nameof(AutoStartOnLogin));
                MessageBox.Show(
                    "Auto-start could not be registered.\n\nThis only works with a published build, not when running via 'dotnet run'.",
                    "Auto-start", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        else
        {
            AutoStartService.Disable();
        }
    }

    [RelayCommand]
    private void SelectCardColor(CardColorSwatchVm swatch)
    {
        SelectedCardColor = swatch;
        UpdateCardSelectionMarkers();
        CardBrushService.Apply(swatch.Hex, CardOpacity);
    }

    partial void OnCardOpacityChanged(int value) =>
        CardBrushService.Apply(SelectedCardColor?.Hex ?? "1E1E1E", value);

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

    private void UpdateCardSelectionMarkers()
    {
        foreach (var s in CardSwatches)
            s.IsSelected = s == SelectedCardColor;
    }

    public void Save()
    {
        _appSettings.DefaultInstallRoot = DefaultInstallRoot?.Trim() ?? "";
        _appSettings.StartMinimized     = StartMinimized;
        _appSettings.CardColor          = SelectedCardColor?.Hex ?? "1E1E1E";
        _appSettings.CardOpacity        = CardOpacity;
        _appSettings.BackgroundImagePath = BackgroundImagePath?.Trim() ?? "";
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

public partial class CardColorSwatchVm : ObservableObject
{
    public string Name { get; }
    public string Hex  { get; }
    public SolidColorBrush Brush { get; }
    [ObservableProperty] private bool _isSelected;

    public CardColorSwatchVm(CardColorSwatch s)
    {
        Name  = s.Name;
        Hex   = s.Hex;
        Brush = s.Brush;
    }
}
