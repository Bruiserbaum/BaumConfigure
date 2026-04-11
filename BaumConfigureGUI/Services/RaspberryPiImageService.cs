using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BaumConfigureGUI.Services;

public record PiCategory(string Name, List<PiImage> Images);

public record PiImage(
    string Name,
    string Description,
    string Url,
    string ReleaseDate,
    long   DownloadSize,   // bytes
    string Devices);

public static class RaspberryPiImageService
{
    private const string ListUrl = "https://downloads.raspberrypi.com/os_list_imagingutility_v3.json";

    private static readonly HttpClient _http = new();

    static RaspberryPiImageService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", $"BaumConfigure/{UpdateService.CurrentVersion}");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public static async Task<List<PiCategory>> FetchCategoriesAsync()
    {
        var root = await _http.GetFromJsonAsync<OsListRoot>(ListUrl)
            ?? throw new InvalidOperationException("Failed to parse Raspberry Pi OS list.");

        var categories = new List<PiCategory>();
        foreach (var entry in root.OsList ?? [])
            FlattenEntry(entry, categories);

        return categories;
    }

    private static void FlattenEntry(OsEntry entry, List<PiCategory> categories)
    {
        var images = new List<PiImage>();

        // Direct downloadable image
        if (!string.IsNullOrEmpty(entry.Url))
            images.Add(ToImage(entry));

        // Nested sub-items (ignore subitems_url — requires extra fetches)
        foreach (var sub in entry.Subitems ?? [])
        {
            if (!string.IsNullOrEmpty(sub.Url))
                images.Add(ToImage(sub));
            // One more level deep
            foreach (var sub2 in sub.Subitems ?? [])
                if (!string.IsNullOrEmpty(sub2.Url))
                    images.Add(ToImage(sub2));
        }

        if (images.Count > 0)
            categories.Add(new PiCategory(entry.Name ?? "Unknown", images));
    }

    private static PiImage ToImage(OsEntry e) => new(
        Name        : e.Name ?? "",
        Description : e.Description ?? "",
        Url         : e.Url ?? "",
        ReleaseDate : e.ReleaseDate ?? "",
        DownloadSize: e.ImageDownloadSize,
        Devices     : string.Join(", ", e.Devices ?? []));

    public static async Task<string> DownloadAsync(
        PiImage         image,
        string          destDir,
        Action<int>     onProgress,
        Action<string>  onLog,
        CancellationToken ct = default)
    {
        var fileName = Path.GetFileName(new Uri(image.Url).LocalPath);
        var destFile = Path.Combine(destDir, fileName);

        onLog($"Downloading {fileName}…");
        using var resp = await _http.GetAsync(image.Url, HttpCompletionOption.ResponseHeadersRead, ct);
        resp.EnsureSuccessStatusCode();

        long? total = resp.Content.Headers.ContentLength;
        await using var src  = await resp.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(destFile);

        var buf = new byte[81920];
        long written = 0;
        int read;
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dest.WriteAsync(buf.AsMemory(0, read), ct);
            written += read;
            if (total > 0) onProgress((int)(written * 100 / total.Value));
        }
        onProgress(100);
        dest.Close();
        onLog("Download complete.");

        if (fileName.EndsWith(".xz", StringComparison.OrdinalIgnoreCase))
        {
            onLog("Decompressing .xz image via WSL…");
            var wslFile = WslService.ToWslPath(destFile);
            var wsl = new WslService();
            await wsl.RunAsync($"xz -d -k -f '{wslFile}'", onLog, ct);
            var imgPath = destFile[..^3]; // strip .xz
            onLog($"Decompressed: {Path.GetFileName(imgPath)}");
            return imgPath;
        }

        return destFile;
    }

    // ── JSON models ───────────────────────────────────────────────────────────
    private sealed class OsListRoot
    {
        [JsonPropertyName("os_list")] public List<OsEntry>? OsList { get; set; }
    }

    private sealed class OsEntry
    {
        [JsonPropertyName("name")]                public string?      Name              { get; set; }
        [JsonPropertyName("description")]         public string?      Description       { get; set; }
        [JsonPropertyName("url")]                 public string?      Url               { get; set; }
        [JsonPropertyName("release_date")]        public string?      ReleaseDate       { get; set; }
        [JsonPropertyName("image_download_size")] public long         ImageDownloadSize { get; set; }
        [JsonPropertyName("devices")]             public List<string>? Devices          { get; set; }
        [JsonPropertyName("subitems")]            public List<OsEntry>? Subitems        { get; set; }
    }
}
