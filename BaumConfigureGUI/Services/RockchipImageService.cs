using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace BaumConfigureGUI.Services;

public record RockchipRelease(string TagName, string PublishedAt, List<RockchipAsset> Assets);
public record RockchipAsset(string Name, long SizeBytes, string DownloadUrl);

public static class RockchipImageService
{
    private const string Owner = "Joshua-Riek";
    private const string Repo  = "ubuntu-rockchip";

    private static readonly HttpClient _http = new();

    static RockchipImageService()
    {
        _http.DefaultRequestHeaders.Add("User-Agent", $"BaumConfigure/{UpdateService.CurrentVersion}");
        _http.Timeout = TimeSpan.FromSeconds(20);
    }

    /// <summary>Fetches the last <paramref name="count"/> releases with .img.xz assets.</summary>
    public static async Task<List<RockchipRelease>> FetchReleasesAsync(int count = 10)
    {
        var raw = await _http.GetFromJsonAsync<List<GhRelease>>(
            $"https://api.github.com/repos/{Owner}/{Repo}/releases?per_page={count}");

        if (raw is null) return [];

        return raw
            .Select(r => new RockchipRelease(
                r.TagName ?? "",
                r.PublishedAt?.Substring(0, 10) ?? "",
                r.Assets?
                  .Where(a => a.Name.EndsWith(".img.xz", StringComparison.OrdinalIgnoreCase)
                           || a.Name.EndsWith(".img",    StringComparison.OrdinalIgnoreCase))
                  .Select(a => new RockchipAsset(a.Name, a.Size, a.BrowserDownloadUrl))
                  .ToList() ?? []))
            .Where(r => r.Assets.Count > 0)
            .ToList();
    }

    /// <summary>
    /// Downloads <paramref name="asset"/> to <paramref name="destPath"/>,
    /// then decompresses if it ends with .xz.
    /// Reports progress 0-100 via <paramref name="onProgress"/>.
    /// Returns the final .img path.
    /// </summary>
    public static async Task<string> DownloadAsync(
        RockchipAsset   asset,
        string          destDir,
        Action<int>     onProgress,
        Action<string>  onLog,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(destDir);
        var destFile = Path.Combine(destDir, asset.Name);

        // ── Download ──────────────────────────────────────────────────────────
        onLog($"Downloading {asset.Name} ({FormatBytes(asset.SizeBytes)})…");
        using var response = await _http.GetAsync(asset.DownloadUrl,
            HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long? total = response.Content.Headers.ContentLength ?? asset.SizeBytes;
        await using var src  = await response.Content.ReadAsStreamAsync(ct);
        await using var dest = File.Create(destFile);

        var buf     = new byte[131072];
        long written = 0;
        int  read;
        while ((read = await src.ReadAsync(buf, ct)) > 0)
        {
            await dest.WriteAsync(buf.AsMemory(0, read), ct);
            written += read;
            if (total > 0)
                onProgress((int)Math.Min(90, written * 90 / total.Value));
        }
        dest.Close();
        onLog($"  Downloaded to {destFile}");

        // ── Decompress .xz ────────────────────────────────────────────────────
        if (!destFile.EndsWith(".xz", StringComparison.OrdinalIgnoreCase))
        {
            onProgress(100);
            return destFile;
        }

        var imgFile = destFile[..^3]; // strip .xz
        onLog($"Decompressing {Path.GetFileName(destFile)}…");
        onLog("  (This may take a minute — the image is large)");

        // Use xz via WSL — no native .NET xz library needed
        var wsl = new WslService("Ubuntu");
        var wslSrc = WslService.ToWslPath(destFile);
        var wslDst = WslService.ToWslPath(imgFile);
        await wsl.RunAsync(
            $"xz -d -k -f '{wslSrc}' && mv '{wslSrc[..^3]}' '{wslDst}'",
            onLog, ct);

        onProgress(100);

        if (!File.Exists(imgFile))
            throw new FileNotFoundException($"Decompressed image not found at {imgFile}");

        onLog($"  Image ready: {imgFile}");
        return imgFile;
    }

    private static string FormatBytes(long b) =>
        b switch
        {
            >= 1_073_741_824 => $"{b / 1_073_741_824.0:F1} GB",
            >= 1_048_576     => $"{b / 1_048_576.0:F0} MB",
            _                => $"{b / 1024} KB",
        };

    // ── GitHub API models ─────────────────────────────────────────────────────
    private sealed class GhRelease
    {
        [JsonPropertyName("tag_name")]     public string? TagName     { get; set; }
        [JsonPropertyName("published_at")] public string? PublishedAt { get; set; }
        [JsonPropertyName("assets")]       public List<GhAsset>? Assets { get; set; }
    }

    private sealed class GhAsset
    {
        [JsonPropertyName("name")]                 public string Name                { get; set; } = "";
        [JsonPropertyName("size")]                 public long   Size                { get; set; }
        [JsonPropertyName("browser_download_url")] public string BrowserDownloadUrl { get; set; } = "";
    }
}
