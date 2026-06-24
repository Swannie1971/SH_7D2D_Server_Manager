using CommunityToolkit.Mvvm.ComponentModel;
using SevenDaysManager.Models;

namespace SevenDaysManager.ViewModels;

public partial class DiscordEventViewModel : ObservableObject
{
    public string EventName { get; }
    public string Variables { get; }
    public string DefaultColor { get; }

    [ObservableProperty] private bool   _enabled         = true;
    [ObservableProperty] private string _webhookOverride  = "";
    [ObservableProperty] private string _message          = "";
    [ObservableProperty] private string _color            = "green";
    [ObservableProperty] private string _roleMentionId    = "";
    [ObservableProperty] private string _thumbnailUrl     = "";
    [ObservableProperty] private int    _cooldownSeconds  = 0;

    public static readonly string[] ColorOptions =
        ["green", "blue", "orange", "red", "purple", "yellow", "grey", "white"];

    public DiscordEventViewModel(string eventName, string variables, DiscordEventConfig cfg)
    {
        EventName    = eventName;
        Variables    = variables;
        DefaultColor = cfg.Color;
        LoadFrom(cfg);
    }

    public void LoadFrom(DiscordEventConfig cfg)
    {
        Enabled         = cfg.Enabled;
        WebhookOverride = cfg.WebhookOverride;
        Message         = cfg.Message;
        Color           = cfg.Color;
        RoleMentionId   = cfg.RoleMentionId;
        ThumbnailUrl    = cfg.ThumbnailUrl;
        CooldownSeconds = cfg.CooldownSeconds;
    }

    public DiscordEventConfig ToConfig() => new()
    {
        Enabled         = Enabled,
        WebhookOverride = WebhookOverride,
        Message         = Message,
        Color           = Color,
        RoleMentionId   = RoleMentionId,
        ThumbnailUrl    = ThumbnailUrl,
        CooldownSeconds = CooldownSeconds,
    };
}
