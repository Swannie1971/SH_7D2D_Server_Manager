using System.Net.Http;
using System.Text;
using System.Text.Json;
using SevenDaysManager.Models;

namespace SevenDaysManager.Services;

public class DiscordService
{
    private static readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(10) };

    // Cooldown tracking: eventKey → last sent UTC
    private readonly Dictionary<string, DateTime> _lastSent = new();

    // ── Color map → Discord embed int ────────────────────────────────────────

    private static readonly Dictionary<string, int> Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["green"]  = 0x57F287,
        ["red"]    = 0xED4245,
        ["orange"] = 0xFEA800,
        ["blue"]   = 0x5865F2,
        ["yellow"] = 0xFEE75C,
        ["purple"] = 0x9B59B6,
        ["grey"]   = 0x95A5A6,
        ["white"]  = 0xFFFFFF,
    };

    private static int ResolveColor(string name) =>
        Colors.TryGetValue(name, out var c) ? c : 0x57F287;

    // ── Format ────────────────────────────────────────────────────────────────

    public string Format(string template, string server,
        int minutes = 0, int players = 0, int maxPlayers = 0,
        string ipPort = "", string reason = "")
    {
        return template
            .Replace("{SERVER}",     server)
            .Replace("{TIMEMIN}",    minutes.ToString())
            .Replace("{PLAYERS}",    players.ToString())
            .Replace("{MAXPLAYERS}", maxPlayers.ToString())
            .Replace("{IPPORT}",     ipPort)
            .Replace("{REASON}",     reason)
            .Replace("\\N",          "\n");
    }

    // ── Send event ────────────────────────────────────────────────────────────

    public async Task SendEventAsync(
        DiscordEventConfig evt,
        string             defaultWebhook,
        string             eventKey,          // unique key for cooldown tracking
        string             serverName,
        int                minutes    = 0,
        int                players    = 0,
        int                maxPlayers = 0,
        string             ipPort     = "",
        string             reason     = "")
    {
        if (!evt.Enabled) return;

        var webhook = !string.IsNullOrWhiteSpace(evt.WebhookOverride)
            ? evt.WebhookOverride : defaultWebhook;
        if (string.IsNullOrWhiteSpace(webhook)) return;

        // Cooldown check
        if (evt.CooldownSeconds > 0 && _lastSent.TryGetValue(eventKey, out var last))
            if ((DateTime.UtcNow - last).TotalSeconds < evt.CooldownSeconds) return;

        var message = Format(evt.Message, serverName, minutes, players, maxPlayers, ipPort, reason);
        var color   = ResolveColor(evt.Color);

        // Build embed
        var embedObj = string.IsNullOrWhiteSpace(evt.ThumbnailUrl)
            ? (object)new { description = message, color }
            : new { description = message, color, thumbnail = new { url = evt.ThumbnailUrl } };

        // Role mention goes in content so it actually pings
        string? content = !string.IsNullOrWhiteSpace(evt.RoleMentionId)
            ? $"<@&{evt.RoleMentionId}>" : null;

        var payload = JsonSerializer.Serialize(new { content, embeds = new[] { embedObj } });

        try
        {
            await _http.PostAsync(webhook,
                new StringContent(payload, Encoding.UTF8, "application/json"));
            _lastSent[eventKey] = DateTime.UtcNow;
        }
        catch { }
    }

    // ── Per-event test ───────────────────────────────────────────────────────

    /// <summary>
    /// Post a single event exactly as it would fire for real (same colour, thumbnail and role
    /// mention), but with sample values — and, unlike SendEventAsync, it REPORTS FAILURE and
    /// ignores both the Enabled flag and the cooldown, so a test always sends and always tells
    /// you whether it worked.
    /// </summary>
    public async Task<bool> SendEventTestAsync(
        DiscordEventConfig evt,
        string             webhookUrl,
        string             serverName,
        string?            logoPath = null)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return false;

        var message = Format(evt.Message, serverName,
            minutes: 10, players: 3, maxPlayers: 8,
            ipPort: "127.0.0.1:26900", reason: "test");

        var color = ResolveColor(evt.Color);

        // {IMAGE} means "the server's own logo" — it's only usable if that's a real URL Discord
        // can fetch; a local file path is meaningless to them, so drop the thumbnail instead.
        var thumb = evt.ThumbnailUrl?.Replace("{IMAGE}", logoPath ?? "");
        if (!Uri.TryCreate(thumb, UriKind.Absolute, out var thumbUri) ||
            (thumbUri.Scheme != Uri.UriSchemeHttp && thumbUri.Scheme != Uri.UriSchemeHttps))
            thumb = null;

        var embedObj = string.IsNullOrWhiteSpace(thumb)
            ? (object)new { description = message, color }
            : new { description = message, color, thumbnail = new { url = thumb } };

        string? content = !string.IsNullOrWhiteSpace(evt.RoleMentionId)
            ? $"<@&{evt.RoleMentionId}>" : null;

        var payload = JsonSerializer.Serialize(new { content, embeds = new[] { embedObj } });

        try
        {
            var resp = await _http.PostAsync(webhookUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // ── Test ─────────────────────────────────────────────────────────────────

    public async Task<bool> TestAsync(string webhookUrl)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return false;
        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                embeds = new[] { new
                {
                    description = "✅ 7D2D Server Manager — Discord integration is working!",
                    color       = 0x57F287
                }}
            });
            var resp = await _http.PostAsync(webhookUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"));
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }

    // Legacy simple send (used by ScheduleService restart warning)
    public async Task SendAsync(string webhookUrl, string message)
    {
        if (string.IsNullOrWhiteSpace(webhookUrl)) return;
        try
        {
            var payload = JsonSerializer.Serialize(new { content = message });
            await _http.PostAsync(webhookUrl,
                new StringContent(payload, Encoding.UTF8, "application/json"));
        }
        catch { }
    }
}
