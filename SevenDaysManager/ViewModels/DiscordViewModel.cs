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
