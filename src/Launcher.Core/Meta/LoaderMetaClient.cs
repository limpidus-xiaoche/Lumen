using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Launcher.Core.Storage;
using Launcher.Protocol;

namespace Launcher.Core.Meta;

/// <summary>
/// 加载器元数据：Fabric / Quilt 走官方 meta，Forge / NeoForge 读 maven 版本清单。
/// NeoForge 官方 maven 在部分网络不可达，改用 BMCLAPI 镜像。
/// </summary>
public sealed class LoaderMetaClient
{
    private const string FabricMeta = "https://meta.fabricmc.net/v2";
    private const string QuiltMeta = "https://meta.quiltmc.org/v3";
    private const string ForgeMetadata =
        "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
    private const string ForgePromotionsUrl =
        "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
    private const string NeoForgeMetadataMirror =
        "https://bmclapi2.bangbang93.com/maven/net/neoforged/neoforge/maven-metadata.xml";
    private const string NeoForgeMetadataOfficial =
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";
    private const string NeoForgeLegacyMetadataMirror =
        "https://bmclapi2.bangbang93.com/maven/net/neoforged/forge/maven-metadata.xml";

    private static readonly Regex NeoForgeGame = new(@"^1\.(\d+)(?:\.(\d+))?$", RegexOptions.Compiled);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly LauncherPaths _paths;
    private readonly HttpClient _http;
    private readonly ConcurrentDictionary<string, IReadOnlyList<LoaderVersionInfo>> _cache = new();

    public LoaderMetaClient(LauncherPaths paths, HttpClient? http = null)
    {
        _paths = paths;
        _http = http ?? CreateHttpClient();
    }

    /// <summary>加载器是否支持从启动器直接安装（Fabric / Quilt 使用官方 profile JSON）。</summary>
    public static bool SupportsInstall(LoaderKind loader) =>
        loader is LoaderKind.Fabric or LoaderKind.Quilt;

    public async Task<IReadOnlyList<LoaderVersionInfo>> GetVersionsAsync(
        LoaderKind loader, string gameVersion, CancellationToken ct)
    {
        if (loader == LoaderKind.Vanilla || string.IsNullOrWhiteSpace(gameVersion))
        {
            return [];
        }

        var key = $"{loader}:{gameVersion}";
        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var result = loader switch
        {
            LoaderKind.Fabric => await GetFabricLikeAsync(FabricMeta, gameVersion, ct),
            LoaderKind.Quilt => await GetFabricLikeAsync(QuiltMeta, gameVersion, ct),
            LoaderKind.Forge => await GetForgeAsync(gameVersion, ct),
            LoaderKind.NeoForge => await GetNeoForgeAsync(gameVersion, ct),
            _ => [],
        };

        if (result.Count > 0)
        {
            _cache[key] = result;
        }

        return result;
    }

    /// <summary>获取 Fabric / Quilt 的 profile JSON（含 inheritsFrom 与需要下载的库）。</summary>
    public async Task<VersionDetail> GetProfileAsync(
        LoaderKind loader, string gameVersion, string loaderVersion, CancellationToken ct)
    {
        var baseUrl = loader switch
        {
            LoaderKind.Fabric => FabricMeta,
            LoaderKind.Quilt => QuiltMeta,
            _ => throw new NotSupportedException($"{loader} 安装暂未支持"),
        };

        var cacheDir = Path.Combine(_paths.CacheDir, "loaders");
        Directory.CreateDirectory(cacheDir);
        var cache = Path.Combine(cacheDir, $"{loader.ToString().ToLowerInvariant()}-{gameVersion}-{loaderVersion}.json");

        string json;
        if (File.Exists(cache))
        {
            json = File.ReadAllText(cache);
        }
        else
        {
            json = await _http.GetStringAsync(
                $"{baseUrl}/versions/loader/{gameVersion}/{loaderVersion}/profile/json", ct);
            AtomicFile.WriteAllText(cache, json);
        }

        return ManifestClient.DeserializeDetail(json);
    }

    private async Task<IReadOnlyList<LoaderVersionInfo>> GetFabricLikeAsync(
        string baseUrl, string gameVersion, CancellationToken ct)
    {
        var url = $"{baseUrl}/versions/loader/{gameVersion}";
        var items = await _http.GetFromJsonAsync<List<FabricLoaderEntry>>(url, JsonOptions, ct) ?? [];

        return items
            .Where(i => i.Loader is { Version.Length: > 0 })
            .Select(i => new LoaderVersionInfo(
                i.Loader!.Version,
                i.Loader.Stable,
                null))
            .ToArray();
    }

    /// <summary>Forge：maven 元数据按「游戏版本-加载器版本」命名，剔除前缀后得到加载器版本号。</summary>
    private async Task<IReadOnlyList<LoaderVersionInfo>> GetForgeAsync(string gameVersion, CancellationToken ct)
    {
        var xml = await GetStringWithFallbackAsync(ForgeMetadata, null, ct);
        var versions = ParseMavenVersions(xml, $"{gameVersion}-", stripPrefix: true);

        var recommended = await TryGetForgeRecommendedAsync(gameVersion, ct);
        return versions
            .Select(v => new LoaderVersionInfo(v, recommended is not null
                ? string.Equals(v, recommended, StringComparison.OrdinalIgnoreCase)
                : true, null))
            .Reverse() // maven 元数据按发布时间升序，界面上新版本在前
            .ToArray();
    }

    private async Task<string?> TryGetForgeRecommendedAsync(string gameVersion, CancellationToken ct)
    {
        try
        {
            var promotions = await _http.GetFromJsonAsync<ForgePromotions>(ForgePromotionsUrl, JsonOptions, ct);
            return promotions?.Promos is not null &&
                   promotions.Promos.TryGetValue($"{gameVersion}-recommended", out var version)
                ? version
                : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>NeoForge：1.21.1 → 21.1.x；1.20.1 的 NeoForge 发布在 net/neoforged/forge（1.20.1-47.x）。</summary>
    private async Task<IReadOnlyList<LoaderVersionInfo>> GetNeoForgeAsync(string gameVersion, CancellationToken ct)
    {
        var match = NeoForgeGame.Match(gameVersion);
        if (!match.Success)
        {
            return [];
        }

        var major = match.Groups[1].Value;
        var minor = match.Groups[2].Success ? match.Groups[2].Value : "0";

        if (major == "20" && minor == "1")
        {
            var legacyXml = await GetStringWithFallbackAsync(NeoForgeLegacyMetadataMirror, null, ct);
            return ParseMavenVersions(legacyXml, "1.20.1-", stripPrefix: true)
                .Select(v => new LoaderVersionInfo(v, true, null))
                .Reverse()
                .ToArray();
        }

        var xml = await GetStringWithFallbackAsync(NeoForgeMetadataMirror, NeoForgeMetadataOfficial, ct);
        var prefix = $"{major}.{minor}.";
        return ParseMavenVersions(xml, prefix, stripPrefix: false)
            .Select(v => new LoaderVersionInfo(v, true, null))
            .Reverse()
            .ToArray();
    }

    private static IReadOnlyList<string> ParseMavenVersions(string xml, string prefix, bool stripPrefix)
    {
        var document = XDocument.Parse(xml);
        var result = new List<string>();
        foreach (var element in document.Descendants().Where(e => e.Name.LocalName == "version"))
        {
            var value = element.Value.Trim();
            if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(stripPrefix ? value[prefix.Length..] : value);
        }

        return result;
    }

    private async Task<string> GetStringWithFallbackAsync(string primary, string? fallback, CancellationToken ct)
    {
        try
        {
            return await _http.GetStringAsync(primary, ct);
        }
        catch (Exception) when (!ct.IsCancellationRequested && fallback is not null)
        {
            return await _http.GetStringAsync(fallback, ct);
        }
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen/0.1 (+https://github.com/limpidus-xiaoche/Lumen)");
        return client;
    }

    private sealed class FabricLoaderEntry
    {
        public FabricLoader? Loader { get; set; }
    }

    private sealed class FabricLoader
    {
        public string Version { get; set; } = string.Empty;
        public bool Stable { get; set; }
    }

    private sealed class ForgePromotions
    {
        public Dictionary<string, string>? Promos { get; set; }
    }
}
