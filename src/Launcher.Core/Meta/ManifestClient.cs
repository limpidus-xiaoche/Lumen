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
    private readonly Func<DownloadSourceMode> _sourceMode;

    public ManifestClient(
        LauncherPaths paths, HttpClient? http = null, Func<DownloadSourceMode>? sourceMode = null)
    {
        _paths = paths;
        _http = http ?? CreateHttpClient();
        _sourceMode = sourceMode ?? (static () => DownloadSourceMode.Auto);
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

        var json = await GetFirstAvailableAsync(BuildManifestCandidates(_sourceMode()), ct);
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
        var candidates = BuildDetailCandidates(url, _sourceMode());
        Exception? first = null;
        foreach (var candidate in candidates)
        {
            try
            {
                return await _http.GetStringAsync(candidate, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                first ??= ex;
            }
        }

        throw first ?? new HttpRequestException($"版本 JSON 下载失败：{url}");
    }

    private async Task<string> GetFirstAvailableAsync(IReadOnlyList<Uri> candidates, CancellationToken ct)
    {
        Exception? first = null;
        foreach (var candidate in candidates)
        {
            try
            {
                return await _http.GetStringAsync(candidate, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                first ??= ex;
            }
        }

        throw first ?? new HttpRequestException("版本清单下载失败");
    }

    /// <summary>版本清单候选地址：Auto 官方优先，Official 仅官方，Mirror 镜像优先且官方兜底。</summary>
    public static IReadOnlyList<Uri> BuildManifestCandidates(DownloadSourceMode mode) => mode switch
    {
        DownloadSourceMode.Official => [OfficialManifest],
        DownloadSourceMode.Mirror => [MirrorManifest, OfficialManifest],
        _ => [OfficialManifest, MirrorManifest],
    };

    /// <summary>版本 JSON 候选地址：Official 仅原 URL，Mirror 先镜像后原 URL，Auto 原 URL 优先。</summary>
    public static IReadOnlyList<string> BuildDetailCandidates(string url, DownloadSourceMode mode)
    {
        var mirror = MapMirror(url);
        var hasMirror = !string.Equals(mirror, url, StringComparison.OrdinalIgnoreCase);
        var result = new List<string>();
        switch (mode)
        {
            case DownloadSourceMode.Official:
                result.Add(url);
                break;
            case DownloadSourceMode.Mirror:
                if (hasMirror)
                {
                    result.Add(mirror);
                }

                result.Add(url);
                break;
            default:
                result.Add(url);
                if (hasMirror)
                {
                    result.Add(mirror);
                }

                break;
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
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
