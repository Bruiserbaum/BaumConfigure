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
    long   DownloadSize,
    string Devices);

public static class RaspberryPiImageService
{
    private const string ListUrl = "https://downloads.raspberrypi.org/os_list_imagingutility_v4.json";

    private static readonly HttpClient _http = new();

    static RaspberryPiImageService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", $"BaumConfigure/{UpdateService.CurrentVersion}");
        _http.Timeout = TimeSpan.FromSeconds(30);
    }

    public static async Task<List<PiCategory>> FetchCategoriesAsync(Action<string>? onStatus = null)
    {
        onStatus?.Invoke("Fetching Raspberry Pi OS image list…");

        var root = await _http.GetFromJsonAsync<OsListRoot>(ListUrl)
            ?? throw new InvalidOperationException("Failed to parse Raspberry Pi OS list.");

        var categories = new List<PiCategory>();
        var tasks = new List<Task>();

        foreach (var entry in root.OsList ?? [])
        {
            // If a subitems_url exists, fetch it in parallel then flatten
            if (!string.IsNullOrEmpty(entry.SubitemsUrl))
            {
                tasks.Add(FetchSubitemsAndAddAsync(entry, categories, onStatus));
            }
            else
            {
                FlattenEntry(entry, categories);
            }
        }

        if (tasks.Count > 0)
            await Task.WhenAll(tasks);

        return [.. categories.OrderBy(c => c.Name)];
    }

    private static async Task FetchSubitemsAndAddAsync(
        OsEntry entry, List<PiCategory> categories, Action<string>? onStatus)
    {
        try
        {
            onStatus?.Invoke($"Fetching {entry.Name}…");
            var subRoot = await _http.GetFromJsonAsync<OsListRoot>(entry.SubitemsUrl!)
                          ?? new OsListRoot();

            // Merge fetched subitems into the entry
            entry.Subitems ??= [];
            entry.Subitems.AddRange(subRoot.OsList ?? []);
        }
        catch { /* network error for optional sub-list — skip */ }

        FlattenEntry(entry, categories);
    }

    private static void FlattenEntry(OsEntry entry, List<PiCategory> categories)
    {
        var images = new List<PiImage>();

        if (!string.IsNullOrEmpty(entry.Url))
            images.Add(ToImage(entry));

        foreach (var sub in entry.Subitems ?? [])
        {
            if (!string.IsNullOrEmpty(sub.Url))
                images.Add(ToImage(sub));

            foreach (var sub2 in sub.Subitems ?? [])
                if (!string.IsNullOrEmpty(sub2.Url))
                    images.Add(ToImage(sub2));
        }

        if (images.Count > 0)
        {
            lock (categories)
                categories.Add(new PiCategory(entry.Name ?? "Unknown", images));
        }
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
        int  read;
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
            var imgPath = destFile[..^3];
            onLog($"Decompressing {Path.GetFileName(destFile)}…");
            onLog("  (This may take a minute — the image is large)");

            // Use xz -d -c (stdout) redirected by bash so xz never touches the
            // output path — avoids "Cannot remove: Is a directory" if a stale
            // directory exists at that location.
            var wslSrc = WslService.ToWslPath(destFile);
            var wslDst = WslService.ToWslPath(imgPath);
            var wsl = new WslService();
            await wsl.RunAsync($"xz -d -c '{wslSrc}' > '{wslDst}'", onLog, ct);
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
        [JsonPropertyName("name")]                public string?       Name              { get; set; }
        [JsonPropertyName("description")]         public string?       Description       { get; set; }
        [JsonPropertyName("url")]                 public string?       Url               { get; set; }
        [JsonPropertyName("release_date")]        public string?       ReleaseDate       { get; set; }
        [JsonPropertyName("image_download_size")] public long          ImageDownloadSize { get; set; }
        [JsonPropertyName("devices")]             public List<string>? Devices           { get; set; }
        [JsonPropertyName("subitems")]            public List<OsEntry>? Subitems         { get; set; }
        [JsonPropertyName("subitems_url")]        public string?       SubitemsUrl       { get; set; }
    }
}
