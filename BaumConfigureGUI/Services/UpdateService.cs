using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BaumConfigureGUI.Services;

public record ReleaseInfo(string Version, string DownloadUrl, string ReleaseNotes);

public static class UpdateService
{
    private const string Owner = "Bruiserbaum";
    private const string Repo  = "BaumConfigure";
    public  const string CurrentVersion = "1.6.1";

    private static readonly HttpClient _http = new();

    static UpdateService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", $"BaumConfigure/{CurrentVersion}");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Checks GitHub for a newer release. Returns null if up to date or on network error.
    /// </summary>
    public static async Task<ReleaseInfo?> CheckAsync()
    {
        try
        {
            var release = await _http.GetFromJsonAsync<GitHubRelease>(
                $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest");

            if (release is null) return null;

            var latest = release.TagName?.TrimStart('v');
            if (latest is null || !IsNewer(latest, CurrentVersion)) return null;

            var url = release.Assets
                ?.FirstOrDefault(a =>
                    a.Name.Contains("Setup", StringComparison.OrdinalIgnoreCase) &&
                    a.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                ?.BrowserDownloadUrl;

            if (url is null) return null;

            return new ReleaseInfo(latest, url, release.Body ?? "");
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads the installer to a temp file, runs it silently, then restarts the app.
    /// Reports download progress 0–100 via <paramref name="onProgress"/>.
    /// </summary>
    public static async Task DownloadAndInstallAsync(
        ReleaseInfo     info,
        Action<int>     onProgress,
        Action<string>  onLog,
        CancellationToken ct = default)
    {
        var tmpPath = Path.Combine(Path.GetTempPath(), $"BaumConfigure-Setup-{info.Version}.exe");

        onLog($"Downloading BaumConfigure v{info.Version}…");
        using var response = await _http.GetAsync(info.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength;
        await using var src  = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(tmpPath);

        var buf     = new byte[81920];
        long written = 0;
        int  read;
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dest.WriteAsync(buf.AsMemory(0, read), ct);
            written += read;
            if (total > 0)
                onProgress((int)(written * 100 / total.Value));
        }
        onProgress(100);
        dest.Close();

        onLog("Installing update…");

        // Run installer silently. The setup.iss [Run] section has a WizardSilent entry
        // that auto-launches the app after install, making restart reliable.
        var psi = new ProcessStartInfo(tmpPath)
        {
            Arguments       = "/VERYSILENT /NORESTART /CLOSEAPPLICATIONS",
            UseShellExecute = true,
        };
        Process.Start(psi);

        // Exit current instance — installer will relaunch it
        Application.Exit();
    }

    /// <summary>Returns true if an update check should run given the schedule settings.</summary>
    public static bool ShouldCheck(bool weeklyOnly, DateTime lastCheck) =>
        !weeklyOnly || (DateTime.UtcNow - lastCheck).TotalDays >= 7;

    private static bool IsNewer(string candidate, string current)
    {
        return Version.TryParse(candidate, out var c)
            && Version.TryParse(current,   out var cur)
            && c > cur;
    }

    // ── GitHub API models ─────────────────────────────────────────────────────
    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string? TagName { get; set; }
        [JsonPropertyName("body")]     public string? Body    { get; set; }
        [JsonPropertyName("assets")]   public List<GitHubAsset>? Assets { get; set; }
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")]                 public string Name                { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
    }
}
