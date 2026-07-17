using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SevenDaysManager.Services;

public static class UpdateService
{
    // The single source of truth for the app version. The sidebar binds to it
    // (MainViewModel.AppVersion), and the update check compares it against the latest
    // GitHub release tag â€” so bump this and tag the release as "v{CurrentVersion}".
    public const string CurrentVersion = "0.3.3";

    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "7D2D-Server-Manager" } }
    };

    public record UpdateInfo(string LatestVersion, string ReleaseUrl, string? ReleaseNotes, string? DownloadUrl);

    public static async Task<UpdateInfo?> CheckAsync()
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GithubRelease>(
                "https://api.github.com/repos/Swannie1971/SH_7D2D_Manager-releases/releases/latest");

            if (release?.TagName is not { } tag) return null;

            var latestStr = tag.TrimStart('v');
            if (!Version.TryParse(latestStr, out var latest)) return null;
            if (!Version.TryParse(CurrentVersion, out var current)) return null;
            if (latest <= current) return null;

            // Pick the asset by NAME, not by position. A release carries two exes: the bare
            // updater exe ("SevenDaysManager.exe") and the full installer
            // ("SevenDaysManager-Setup-x.y.z.exe"). The in-place updater must grab the FORMER â€”
            // downloading the installer would launch a setup wizard instead of a silent swap.
            //
            // Relying on order does NOT work: GitHub sorts assets, and "-Setup" sorts before
            // the bare name, so "the first .exe" is the installer. Match the exact updater name
            // and explicitly rule out anything that looks like an installer.
            var exes = release.Assets?
                .Where(a => a.Name?.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) == true)
                .ToList() ?? new List<GithubAsset>();

            var updater =
                exes.FirstOrDefault(a =>
                    string.Equals(a.Name, "SevenDaysManager.exe", StringComparison.OrdinalIgnoreCase))
                ?? exes.FirstOrDefault(a =>
                    a.Name?.Contains("setup", StringComparison.OrdinalIgnoreCase) != true &&
                    a.Name?.Contains("install", StringComparison.OrdinalIgnoreCase) != true);

            var downloadUrl = updater?.BrowserDownloadUrl;

            return new UpdateInfo(latestStr, release.HtmlUrl ?? "", release.Body, downloadUrl);
        }
        catch
        {
            return null;
        }
    }

    private sealed class GithubRelease
    {
        [JsonPropertyName("tag_name")]  public string?        TagName { get; set; }
        [JsonPropertyName("html_url")]  public string?        HtmlUrl { get; set; }
        [JsonPropertyName("body")]      public string?        Body    { get; set; }
        [JsonPropertyName("assets")]    public GithubAsset[]? Assets  { get; set; }
    }

    private sealed class GithubAsset
    {
        [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
        [JsonPropertyName("name")]                 public string? Name                { get; set; }
    }
}
