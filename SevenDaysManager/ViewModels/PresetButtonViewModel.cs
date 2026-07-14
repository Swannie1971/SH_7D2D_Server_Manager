using CommunityToolkit.Mvvm.ComponentModel;
using SevenDaysManager.Models;

namespace SevenDaysManager.ViewModels;

/// <summary>
/// A preset button. Wraps the preset so the button can show whether it's the one currently
/// loaded — a bare SandboxPreset record has nowhere to hang that state.
/// </summary>
public partial class PresetButtonViewModel : ObservableObject
{
    public SandboxPreset Preset { get; }

    public string Name     => Preset.Name;
    public double Severity => Preset.Severity;

    /// <summary>
    /// True when the current settings match this preset EXACTLY.
    ///
    /// Goes false the moment any option is changed — including options this preset never
    /// touched (bump the air drops on Scavenger and it's no longer Scavenger). That's what
    /// drives the CUSTOM badge.
    /// </summary>
    [ObservableProperty] private bool _isActive;

    public PresetButtonViewModel(SandboxPreset preset) => Preset = preset;
}
