using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SevenDaysManager.Models;

namespace SevenDaysManager.ViewModels;

public partial class DiscordEventViewModel : ObservableObject
{
    public string EventName { get; }
    public string Variables { get; }
    public string DefaultColor { get; }

    // Supplied by the parent DiscordViewModel: the event VM owns no server or service, so
    // per-card Save / Send Test delegate back up to it.
    private Func<Task<bool>>? _sendTest;
    private Action?           _save;

    /// <summary>Per-card status line ("Sending…", "✓ Sent", "✗ Failed", "✓ Saved").</summary>
    [ObservableProperty] private string _status = "";

    internal void WireActions(Func<Task<bool>> sendTest, Action save)
    {
        _sendTest = sendTest;
        _save     = save;
    }

    [RelayCommand]
    private void SaveEvent()
    {
        _save?.Invoke();
        Status = $"✓ Saved  ·  {DateTime.Now:t}";
    }

    [RelayCommand]
    private async Task SendTestAsync()
    {
        if (_sendTest is null) return;

        Status = "Sending…";
        var ok = await _sendTest();
        Status = ok
            ? $"✓ Sent  ·  {DateTime.Now:t}"
            : "✗ Failed — check the webhook URL";
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewMessage))]
    private string _message = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewAccent))]
    private string _color = "green";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasThumbnail))]
    [NotifyPropertyChangedFor(nameof(PreviewThumbnail))]
    private string _thumbnailUrl = "";

    [ObservableProperty] private bool   _enabled          = true;
    [ObservableProperty] private string _webhookOverride  = "";
    [ObservableProperty] private string _roleMentionId    = "";
    [ObservableProperty] private int    _cooldownSeconds  = 0;

    public static readonly string[] ColorOptions =
        ["green", "blue", "orange", "red", "purple", "yellow", "grey", "white"];

    // ── Live preview ──────────────────────────────────────────────────────────
    // Sample values used to render the preview. They only ever feed the preview — the real
    // send path substitutes live data in DiscordService.Format().
    private const string SampleServer     = "Test Server Alpha";
    private const string SampleIpPort     = "127.0.0.1:26900";
    private const int    SamplePlayers    = 3;
    private const int    SampleMaxPlayers = 8;
    private const int    SampleMinutes    = 10;
    private const string SampleReason     = "scheduled restart";

    /// <summary>The message as Discord will render it, with sample values substituted.</summary>
    public string PreviewMessage => Substitute(Message);

    /// <summary>Preview embed title, mirroring how the webhook titles the post.</summary>
    public string PreviewTitle => $"{SampleServer} — {EventName} (preview)";

    public string PreviewTimestamp => $"Today at {DateTime.Now:h:mm tt}";

    public bool HasThumbnail => PreviewThumbnail is not null;

    /// <summary>
    /// The thumbnail as a usable image source, or null. Bound directly, a malformed or
    /// unreachable URL makes WPF throw during binding and takes the card down — so it is
    /// validated here and anything unusable simply hides the thumbnail.
    /// {IMAGE} is a send-time placeholder for the server logo, so it has no preview.
    /// </summary>
    public System.Windows.Media.Imaging.BitmapImage? PreviewThumbnail
    {
        get
        {
            var url = ThumbnailUrl?.Trim();
            if (string.IsNullOrEmpty(url) || url.Contains("{IMAGE}")) return null;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return null;

            try
            {
                var bmp = new System.Windows.Media.Imaging.BitmapImage();
                bmp.BeginInit();
                bmp.UriSource   = uri;
                bmp.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                bmp.CreateOptions = System.Windows.Media.Imaging.BitmapCreateOptions.IgnoreImageCache;
                bmp.EndInit();
                return bmp;
            }
            catch { return null; }   // unreachable/invalid — just don't show a thumbnail
        }
    }

    /// <summary>
    /// The Discord embed colour as a real Brush, so the preview's left bar matches the actual
    /// post. Exposed as a Brush (not a hex string) — binding a string to SolidColorBrush.Color
    /// relies on a type conversion that fails silently and leaves the bar blank.
    /// </summary>
    public System.Windows.Media.Brush PreviewAccent
    {
        get
        {
            var hex = Color switch
            {
                "red"    => "#ED4245",
                "orange" => "#FEA800",
                "blue"   => "#5865F2",
                "yellow" => "#FEE75C",
                "purple" => "#9B59B6",
                "grey"   => "#95A5A6",
                "white"  => "#FFFFFF",
                _        => "#57F287",   // green — matches DiscordService's fallback
            };

            var brush = new System.Windows.Media.SolidColorBrush(
                (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex)!);
            brush.Freeze();
            return brush;
        }
    }

    /// <summary>
    /// Mirrors DiscordService.Format() so the preview cannot drift from what is actually sent.
    /// If a token is added there, add it here too.
    /// </summary>
    private static string Substitute(string template) =>
        (template ?? "")
            .Replace("{SERVER}",     SampleServer)
            .Replace("{TIMEMIN}",    SampleMinutes.ToString())
            .Replace("{PLAYERS}",    SamplePlayers.ToString())
            .Replace("{MAXPLAYERS}", SampleMaxPlayers.ToString())
            .Replace("{IPPORT}",     SampleIpPort)
            .Replace("{REASON}",     SampleReason)
            .Replace("\\N",          "\n");

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
