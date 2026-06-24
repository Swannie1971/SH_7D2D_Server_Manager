namespace SevenDaysManager.Models;

public class DiscordConfig
{
    public bool   Enabled        { get; set; } = false;
    public string DefaultWebhook { get; set; } = "";

    public DiscordEventConfig EventServerStart   { get; set; } = new()
    {
        Message = "🟢 **{SERVER}** is online! ({PLAYERS}/{MAXPLAYERS} players)",
        Color   = "green"
    };
    public DiscordEventConfig EventServerStop    { get; set; } = new()
    {
        Message = "⏹️ **{SERVER}** has been stopped.",
        Color   = "orange"
    };
    public DiscordEventConfig EventServerRestart { get; set; } = new()
    {
        Message = "🔄 **{SERVER}** has restarted and is back online!",
        Color   = "blue"
    };
    public DiscordEventConfig EventServerCrash   { get; set; } = new()
    {
        Message = "🚨 **{SERVER}** has gone offline unexpectedly!",
        Color   = "red"
    };
    public DiscordEventConfig EventServerUpdated { get; set; } = new()
    {
        Message = "🆙 **{SERVER}** has been updated to the latest version!",
        Color   = "purple"
    };
    public DiscordEventConfig EventRestartWarn   { get; set; } = new()
    {
        Message = "⚠️ **{SERVER}** restarts in **{TIMEMIN} min**. Wrap up and find shelter!",
        Color   = "yellow"
    };
}
