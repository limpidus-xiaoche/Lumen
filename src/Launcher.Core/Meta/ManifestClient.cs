using System.Net.Http.Json;
using System.Text.Json;
using Launcher.Core.Storage;
using Launcher.Protocol;

namespace Launcher.Core.Meta;

public sealed class ManifestClient
{
    private static readonly Uri OfficialManifest =
        new("https://piston-meta.mojang.com/mc/game/version_manifest_v2.json");

    private static readonly Uri MirrorManifest =
        new("https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    private readonly LauncherPaths _paths;
    private readonly HttpClient _http;

    public ManifestClient(LauncherPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http ?? CreateHttpClient();
    }

    public async Task<VersionManifest> GetManifestAsync(bool force, CancellationToken ct = default)
    {
        var cache = Path.Combine(_paths.CacheDir, "version_manifest_v2.json");
        Directory.CreateDirectory(_paths.CacheDir);

        if (!force && File.Exists(cache) &&
            DateTime.UtcNow - File.GetLastWriteTimeUtc(cache) < TimeSpan.FromMinutes(15))
        {
            return ParseManifest(File.ReadAllText(cache));
        }

        string json;
        try
        {
            json = await _http.GetStringAsync(OfficialManifest, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            json = await _http.GetStringAsync(MirrorManifest, ct);
        }

        AtomicFile.WriteAllText(cache, json);
        return ParseManifest(json);
    }

    public async Task<VersionDetail> GetVersionDetailAsync(VersionSummary summary, CancellationToken ct = default)
    {
        var local = Path.Combine(_paths.SharedDir, "versions", summary.Id, $"{summary.Id}.json");
        if (File.Exists(local))
        {
            return DeserializeDetail(File.ReadAllText(local));
        }

        var cacheDir = Path.Combine(_paths.CacheDir, "versions");
        Directory.CreateDirectory(cacheDir);
        var cache = Path.Combine(cacheDir, $"{SafeId(summary.Id)}.json");
        if (File.Exists(cache))
        {
            return DeserializeDetail(File.ReadAllText(cache));
        }

        var json = await GetWithMirrorAsync(summary.Url, ct);
        AtomicFile.WriteAllText(cache, json);
        return DeserializeDetail(json);
    }

    public async Task<VersionDetail> GetVersionDetailAsync(string versionId, CancellationToken ct = default)
    {
        var manifest = await GetManifestAsync(force: false, ct);
        var summary = manifest.Versions.FirstOrDefault(v => v.Id == versionId)
            ?? throw new InvalidOperationException($"清单中不存在版本 {versionId}");
        return await GetVersionDetailAsync(summary, ct);
    }

    public static VersionManifest ParseManifest(string json) =>
        JsonSerializer.Deserialize<VersionManifest>(json, JsonOptions)
        ?? throw new JsonException("version manifest 解析结果为空");

    public static VersionDetail DeserializeDetail(string json) =>
        JsonSerializer.Deserialize<VersionDetail>(json, JsonOptions)
        ?? throw new JsonException("version detail 解析结果为空");

    private async Task<string> GetWithMirrorAsync(string url, CancellationToken ct)
    {
        try
        {
            return await _http.GetStringAsync(url, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested)
        {
            var mirror = MapMirror(url);
            if (string.Equals(mirror, url, StringComparison.OrdinalIgnoreCase))
            {
                throw;
            }

            return await _http.GetStringAsync(mirror, ct);
        }
    }

    public static string MapMirror(string url) =>
        url.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
           .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase);

    private static string SafeId(string id) =>
        string.Concat(id.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(25) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen/0.1 (+https://github.com/limpidus-xiaoche/Lumen)");
        return client;
    }
}
