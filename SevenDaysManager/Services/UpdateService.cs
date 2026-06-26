using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace SevenDaysManager.Services;

public static class UpdateService
{
    // Bump this constant with every release and tag on GitHub as "v{CurrentVersion}"
    public const string CurrentVersion = "0.2.2";

    private static readonly HttpClient _http = new()
    {
        DefaultRequestHeaders = { { "User-Agent", "7D2D-Server-Manager" } }
    };

    public record UpdateInfo(string LatestVersion, string ReleaseUrl, string? ReleaseNotes);

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

            return new UpdateInfo(latestStr, release.HtmlUrl ?? "", release.Body);
        }
        catch
        {
            return null;
        }
    }

    private sealed class GithubRelease
    {
        [JsonPropertyName("tag_name")]  public string? TagName { get; set; }
        [JsonPropertyName("html_url")]  public string? HtmlUrl { get; set; }
        [JsonPropertyName("body")]      public string? Body    { get; set; }
    }
}
