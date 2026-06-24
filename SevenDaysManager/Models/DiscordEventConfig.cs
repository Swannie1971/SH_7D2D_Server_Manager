namespace SevenDaysManager.Models;

public class DiscordEventConfig
{
    public bool   Enabled         { get; set; } = true;
    public string WebhookOverride { get; set; } = "";   // empty = use default webhook
    public string Message         { get; set; } = "";
    public string Color           { get; set; } = "green";
    public string RoleMentionId   { get; set; } = "";   // Discord role ID to ping
    public string ThumbnailUrl    { get; set; } = "";   // {IMAGE} = server logo
    public int    CooldownSeconds { get; set; } = 0;    // 0 = no cooldown
}
