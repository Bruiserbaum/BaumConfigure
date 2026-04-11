using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BaumConfigureGUI.Services;

/// <summary>
/// Fetches Ubuntu preinstalled Raspberry Pi images from Ubuntu's Simplestreams
/// metadata at cdimage.ubuntu.com. Returns <see cref="PiCategory"/> / <see cref="PiImage"/>
/// so results can be merged directly into <see cref="RaspberryPiImageService"/> results.
/// </summary>
public static class UbuntuRpiImageService
{
    private const string StreamsUrl =
        "https://cdimage.ubuntu.com/releases/streams/v1/com.ubuntu.cdimage:released:download.json";
    private const string BaseUrl = "https://cdimage.ubuntu.com/";

    private static readonly HttpClient _http = new();

    static UbuntuRpiImageService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", $"BaumConfigure/{UpdateService.CurrentVersion}");
        _http.Timeout = TimeSpan.FromSeconds(45);
    }

    /// <summary>
    /// Returns Ubuntu Raspberry Pi image categories, merged by release title.
    /// Each category groups server and desktop variants for that release.
    /// </summary>
    public static async Task<List<PiCategory>> FetchCategoriesAsync(Action<string>? onStatus = null)
    {
        onStatus?.Invoke("Fetching Ubuntu Raspberry Pi images…");

        var root = await _http.GetFromJsonAsync<StreamsRoot>(StreamsUrl)
            ?? throw new InvalidOperationException("Failed to parse Ubuntu image streams.");

        // Key = "Ubuntu 24.04 LTS" → list of PiImage
        var byRelease = new Dictionary<string, List<PiImage>>(StringComparer.OrdinalIgnoreCase);

        foreach (var (productId, product) in root.Products ?? [])
        {
            // arm64 raspi images only
            if (!string.Equals(product.Arch, "arm64", StringComparison.OrdinalIgnoreCase)) continue;
            if (!IsRasPiSubarch(product.Subarch)) continue;

            // Latest version entry
            var latestVersion = product.Versions?
                .OrderByDescending(v => v.Key)
                .Select(v => v.Value)
                .FirstOrDefault();

            if (latestVersion?.Items == null) continue;
            if (!latestVersion.Items.TryGetValue("img.xz", out var item)) continue;
            if (string.IsNullOrEmpty(item.Path)) continue;

            // Detect desktop vs server from the file path (most reliable)
            bool isDesktop = item.Path.Contains("desktop", StringComparison.OrdinalIgnoreCase);
            string variant  = isDesktop ? "Desktop" : "Server";

            var releaseTitle = product.ReleaseTitle ?? product.Release ?? "Unknown";
            var catKey       = $"Ubuntu {releaseTitle}";
            var url          = BaseUrl + item.Path;

            // Friendly name: "Ubuntu 24.04 LTS Preinstalled Server (arm64)"
            var name = $"Ubuntu {releaseTitle} Preinstalled {variant} (arm64)";

            var img = new PiImage(
                Name        : name,
                Description : $"Ubuntu {releaseTitle} preinstalled {variant.ToLower()} image for Raspberry Pi",
                Url         : url,
                ReleaseDate : ParseReleaseDate(item.Path),
                DownloadSize: item.Size,
                Devices     : "Raspberry Pi");

            if (!byRelease.TryGetValue(catKey, out var list))
                byRelease[catKey] = list = [];

            // Deduplicate: skip if we already have the same variant in this release
            if (!list.Any(i => i.Name == name))
                list.Add(img);
        }

        return byRelease
            .OrderByDescending(kv => kv.Key)   // newest release first
            .Select(kv => new PiCategory(
                kv.Key,
                kv.Value.OrderBy(i => i.Name).ToList()))
            .ToList();
    }

    // raspi (Pi 4/5), raspi3 (Pi 3), raspi3+ (Pi 3B+) are all valid ARM64 targets
    private static bool IsRasPiSubarch(string? sub) =>
        sub is "raspi" or "raspi3" or "raspi3+" or "raspi4" or "raspi5";

    // Extract "YYYY-MM-DD" from a path like "releases/24.04.2/release/ubuntu-24.04.2-..."
    private static string ParseReleaseDate(string path)
    {
        // Try to extract point-release version (24.04.2) from path
        var parts = path.Split('/');
        foreach (var p in parts)
            if (p.Length > 4 && char.IsDigit(p[0]) && p.Contains('.'))
                return p;
        return "";
    }

    // ── Simplestreams JSON models ──────────────────────────────────────────────
    private sealed class StreamsRoot
    {
        [JsonPropertyName("products")]
        public Dictionary<string, StreamProduct>? Products { get; set; }
    }

    private sealed class StreamProduct
    {
        [JsonPropertyName("arch")]          public string? Arch         { get; set; }
        [JsonPropertyName("subarch")]       public string? Subarch      { get; set; }
        [JsonPropertyName("release")]       public string? Release      { get; set; }
        [JsonPropertyName("release_title")] public string? ReleaseTitle { get; set; }
        [JsonPropertyName("versions")]
        public Dictionary<string, StreamVersion>? Versions { get; set; }
    }

    private sealed class StreamVersion
    {
        [JsonPropertyName("items")]   public Dictionary<string, StreamItem>? Items   { get; set; }
        [JsonPropertyName("pubname")] public string?                         PubName { get; set; }
    }

    private sealed class StreamItem
    {
        [JsonPropertyName("path")]   public string? Path   { get; set; }
        [JsonPropertyName("size")]   public long    Size   { get; set; }
        [JsonPropertyName("sha256")] public string? Sha256 { get; set; }
    }
}
