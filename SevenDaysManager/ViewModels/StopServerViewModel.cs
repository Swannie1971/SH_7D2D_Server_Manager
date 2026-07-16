using CommunityToolkit.Mvvm.ComponentModel;

namespace SevenDaysManager.ViewModels;

public partial class StopServerViewModel : ObservableObject
{
    [ObservableProperty] private bool _presetNow = true;
    [ObservableProperty] private bool _preset30S;
    [ObservableProperty] private bool _preset1M;
    [ObservableProperty] private bool _preset5M;
    [ObservableProperty] private bool _preset10M;
    [ObservableProperty] private bool _presetCustom;

    [ObservableProperty] private string _customSeconds = "60";
    [ObservableProperty] private string _message = "Server is shutting down.";

    // Only one preset RadioButton is ever checked at a time (WPF handles the mutual
    // exclusion via GroupName), so whichever ObservableProperty setter fires last is the
    // one the user picked — no need to track "which one changed" separately.
    partial void OnPresetNowChanged(bool value)    { if (value) ClearOthers(nameof(PresetNow)); }
    partial void OnPreset30SChanged(bool value)    { if (value) ClearOthers(nameof(Preset30S)); }
    partial void OnPreset1MChanged(bool value)     { if (value) ClearOthers(nameof(Preset1M)); }
    partial void OnPreset5MChanged(bool value)     { if (value) ClearOthers(nameof(Preset5M)); }
    partial void OnPreset10MChanged(bool value)    { if (value) ClearOthers(nameof(Preset10M)); }
    partial void OnPresetCustomChanged(bool value) { if (value) ClearOthers(nameof(PresetCustom)); }

    private void ClearOthers(string keep)
    {
        if (keep != nameof(PresetNow))    PresetNow    = false;
        if (keep != nameof(Preset30S))    Preset30S    = false;
        if (keep != nameof(Preset1M))     Preset1M     = false;
        if (keep != nameof(Preset5M))     Preset5M     = false;
        if (keep != nameof(Preset10M))    Preset10M    = false;
        if (keep != nameof(PresetCustom)) PresetCustom = false;
    }

    /// <summary>Resolves the chosen preset (or custom field) into a delay in seconds.</summary>
    public int GetDelaySeconds()
    {
        if (Preset30S) return 30;
        if (Preset1M)  return 60;
        if (Preset5M)  return 300;
        if (Preset10M) return 600;
        if (PresetCustom)
            return int.TryParse(CustomSeconds, out var s) && s > 0 ? s : 0;
        return 0; // NOW
    }
}
