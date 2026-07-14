using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;
using SevenDaysManager.Services;

namespace SevenDaysManager.ViewModels;

public partial class DiscordViewModel : ObservableObject, IAsyncDisposable
{
    private readonly Server         _server;
    private readonly DiscordService _discord = new();

    [ObservableProperty] private bool   _enabled;
    [ObservableProperty] private string _defaultWebhook = "";
    [ObservableProperty] private string _testStatus     = "";

    public DiscordEventViewModel EvtServerStart   { get; }
    public DiscordEventViewModel EvtServerStop    { get; }
    public DiscordEventViewModel EvtServerRestart { get; }
    public DiscordEventViewModel EvtServerCrash   { get; }
    public DiscordEventViewModel EvtServerUpdated { get; }
    public DiscordEventViewModel EvtRestartWarn   { get; }

    public DiscordViewModel(Server server)
    {
        _server = server;
        server.Discord ??= new DiscordConfig();

        var cfg = server.Discord;
        Enabled        = cfg.Enabled;
        DefaultWebhook = cfg.DefaultWebhook;

        EvtServerStart   = new DiscordEventViewModel("Server online",
            "{SERVER}  {IPPORT}  {PLAYERS}  {MAXPLAYERS}", cfg.EventServerStart);
        EvtServerStop    = new DiscordEventViewModel("Server offline",
            "{SERVER}  {IPPORT}  {REASON}", cfg.EventServerStop);
        EvtServerRestart = new DiscordEventViewModel("Server restarted",
            "{SERVER}  {IPPORT}", cfg.EventServerRestart);
        EvtServerCrash   = new DiscordEventViewModel("Server crashed",
            "{SERVER}  {IPPORT}", cfg.EventServerCrash);
        EvtServerUpdated = new DiscordEventViewModel("Server updated",
            "{SERVER}", cfg.EventServerUpdated);
        EvtRestartWarn   = new DiscordEventViewModel("Restart warning",
            "{SERVER}  {TIMEMIN}", cfg.EventRestartWarn);

        // Give each card its own Save / Send Test. Saving one event still persists the whole
        // DiscordConfig (it's one document), but Send Test posts THAT event only — using its own
        // webhook override, colour, thumbnail and role mention, which the global test can't do.
        foreach (var evt in AllEvents)
            evt.WireActions(() => SendEventTestAsync(evt), Save);
    }

    private IEnumerable<DiscordEventViewModel> AllEvents
    {
        get
        {
            yield return EvtServerStart;
            yield return EvtServerStop;
            yield return EvtServerRestart;
            yield return EvtServerCrash;
            yield return EvtServerUpdated;
            yield return EvtRestartWarn;
        }
    }

    /// <summary>
    /// Post a single event to Discord exactly as it would fire for real — same webhook
    /// resolution, colour, thumbnail and role mention — but with sample values, and bypassing
    /// the cooldown so repeated tests aren't silently swallowed.
    /// </summary>
    private async Task<bool> SendEventTestAsync(DiscordEventViewModel evt)
    {
        var cfg = evt.ToConfig();

        var webhook = !string.IsNullOrWhiteSpace(cfg.WebhookOverride)
            ? cfg.WebhookOverride
            : DefaultWebhook;

        if (string.IsNullOrWhiteSpace(webhook)) return false;

        return await _discord.SendEventTestAsync(
            cfg, webhook, _server.Name, logoPath: _server.LogoPath);
    }

    [RelayCommand]
    private void Save()
    {
        _server.Discord = new DiscordConfig
        {
            Enabled        = Enabled,
            DefaultWebhook = DefaultWebhook,
            EventServerStart   = EvtServerStart  .ToConfig(),
            EventServerStop    = EvtServerStop   .ToConfig(),
            EventServerRestart = EvtServerRestart.ToConfig(),
            EventServerCrash   = EvtServerCrash  .ToConfig(),
            EventServerUpdated = EvtServerUpdated.ToConfig(),
            EventRestartWarn   = EvtRestartWarn  .ToConfig(),
        };
        App.DataStore.SaveServer(_server);
        TestStatus = "✓ Saved";
    }

    [RelayCommand]
    private async Task TestDiscordAsync()
    {
        TestStatus = "Sending…";
        var ok = await _discord.TestAsync(DefaultWebhook);
        TestStatus = ok ? "✓ Test message sent" : "✗ Failed — check the webhook URL";
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
