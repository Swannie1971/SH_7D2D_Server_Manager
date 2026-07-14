using CommunityToolkit.Mvvm.ComponentModel;
using SevenDaysManager.Models;

namespace SevenDaysManager.ViewModels;

/// <summary>One choice in a sandbox dropdown: the value index, and how we label it.</summary>
public record SandboxChoice(int Index, string Label);

/// <summary>
/// A single sandbox option bound to a ComboBox.
///
/// The choices come straight from the game's own value list for that option, so the UI
/// structurally cannot produce a value the game doesn't accept — that's what keeps a
/// hand-built SandboxCode from ever being invalid.
/// </summary>
public partial class SandboxOptionViewModel : ObservableObject
{
    public SandboxOption Option { get; }

    public string Name    => Option.Name;
    public string Display => Option.Display;

    public IReadOnlyList<SandboxChoice> Choices { get; }

    [ObservableProperty] private SandboxChoice _selected;

    /// <summary>Index into the option's value list — i.e. what gets encoded.</summary>
    public int SelectedIndex => Selected?.Index ?? Option.DefaultIndex;

    public bool IsDefault => SelectedIndex == Option.DefaultIndex;

    public SandboxOptionViewModel(SandboxOption option, int? valueIndex = null)
    {
        Option = option;

        Choices = option.Values
            .Select((v, i) => new SandboxChoice(i, Label(option, v, i)))
            .ToList();

        var idx = valueIndex ?? option.DefaultIndex;
        if (idx < 0 || idx >= Choices.Count) idx = Math.Max(0, option.DefaultIndex);
        _selected = Choices[idx];
    }

    public void SetIndex(int index)
    {
        if (index < 0 || index >= Choices.Count) index = Math.Max(0, Option.DefaultIndex);
        Selected = Choices[index];
    }

    /// <summary>
    /// Turn a raw value into something a human can act on. The game stores bare numbers —
    /// "ZombieMove = 3" means nothing to an admin, but "Sprint" does.
    /// </summary>
    private static string Label(SandboxOption o, double v, int index)
    {
        var isDefault = index == o.DefaultIndex;
        var text = FriendlyName(o.Name, v) ?? Number(o, v);
        return isDefault ? $"{text} — default" : text;
    }

    private static string Number(SandboxOption o, double v)
    {
        if (o.IsBool) return v != 0 ? "Enabled" : "Disabled";
        if (o.IsPercent) return $"{v * 100:0.##}%";

        // Multipliers read far better as "1.5x" than "1.5".
        if (o.Values.Count > 2 && o.Values.Contains(1) && o.Values.Any(x => x is > 1 or < 1 and > 0))
            return $"{v:0.##}x";

        return v % 1 == 0 ? ((long)v).ToString() : v.ToString("0.##");
    }

    /// <summary>Named enums the game exposes as bare integers.</summary>
    private static string? FriendlyName(string option, double v) => option switch
    {
        "ZombieMove" or "ZombieMoveNight" or "ZombieFeralMove" or "ZombieBMMove" => (int)v switch
        {
            0 => "Walk", 1 => "Jog", 2 => "Run", 3 => "Sprint", 4 => "Nightmare", _ => null
        },
        "AISmellMode" => (int)v switch
        {
            0 => "None", 1 => "Sense", 2 => "Walk", 3 => "Run", 4 => "Sprint", 5 => "Nightmare", _ => null
        },
        "ZombieFeralSense" => (int)v switch
        {
            0 => "Off", 1 => "Day", 2 => "Night", 3 => "All", _ => null
        },
        "DropOnDeath" => (int)v switch
        {
            0 => "Nothing", 1 => "Everything", 2 => "Toolbelt only",
            3 => "Backpack only", 4 => "Delete all", _ => null
        },
        "DropOnQuit" => (int)v switch
        {
            0 => "Nothing", 1 => "Everything", 2 => "Toolbelt only", 3 => "Backpack only", _ => null
        },
        "BloodMoonFrequency" => (int)v == 0 ? "Disabled" : $"Every {(int)v} days",
        "BloodMoonRange"     => (int)v == 0 ? "Exact day" : $"± {(int)v} days",
        "BloodMoonWarning"   => (int)v switch { 0 => "Off", 1 => "On", 2 => "Verbose", _ => null },
        "BloodMoonEnemyCount"=> $"{(int)v} zombies",
        "LootRespawnDays"    => (int)v < 0 ? "Never" : $"{(int)v} days",
        "AirDropFrequency"   => (int)v == 0 ? "Never" : $"Every {(int)v} game days",
        "DayNightLength"     => $"{(int)v} min",
        "DayLightLength"     => $"{(int)v} h daylight",
        "HeadshotMode"       => (int)v switch
        {
            0 => "None", 1 => "Headshot only", 2 => "Headshot finisher", _ => null
        },
        "DeathPenalty" => (int)v switch
        {
            0 => "None", 1 => "XP debuff", 2 => "Injured", 3 => "Permadeath", _ => null
        },
        _ => null,
    };
}
