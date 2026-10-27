using System.Text.Json;
using System.Xml.Linq;
using Launcher.Core.Download;
using Launcher.Core.Install;
using Launcher.Protocol;

namespace Launcher.Core.Meta;

/// <summary>
/// 加载器版本清单。全部来自官方公开接口，不做本地硬编码版本号。
/// </summary>
public sealed class LoaderCatalog
{
    private const string FabricLoaderUrl = "https://meta.fabricmc.net/v2/versions/loader/{0}";
    private const string FabricProfileUrl = "https://meta.fabricmc.net/v2/versions/loader/{0}/{1}/profile/json";
    private const string ForgeMetadataUrl =
        "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
    private const string ForgePromotionsUrl =
        "https://files.minecraftforge.net/net/minecraftforge/forge/promotions_slim.json";
    private const string NeoForgeMetadataUrl =
        "https://maven.neoforged.net/releases/net/neoforged/neoforge/maven-metadata.xml";

    private readonly HttpClient _http;

    public LoaderCatalog(HttpClient? http = null)
    {
        _http = http ?? CreateHttpClient();
    }

    public async Task<IReadOnlyList<LoaderVersionInfo>> GetVersionsAsync(
        LoaderKind loader, string gameVersion, CancellationToken ct)
    {
        switch (loader)
        {
            case LoaderKind.Fabric:
                return ParseFabricVersions(await GetStringAsync(
                    string.Format(FabricLoaderUrl, gameVersion), ct));
            case LoaderKind.Forge:
            {
                var xml = await GetStringAsync(ForgeMetadataUrl, ct);
                var recommended = await TryGetForgeRecommendedAsync(gameVersion, ct);
                return ParseForgeVersions(xml, gameVersion, recommended);
            }
            case LoaderKind.NeoForge:
                return ParseNeoForgeVersions(await GetStringAsync(NeoForgeMetadataUrl, ct), gameVersion);
            default:
                throw new InstallException("E3001", $"{loader} 尚未支持加载器版本查询");
        }
    }

    public static string FabricProfileUrlFor(string gameVersion, string loaderVersion) =>
        string.Format(FabricProfileUrl, gameVersion, loaderVersion);

    public static string ForgeInstallerUrl(string gameVersion, string loaderVersion) =>
        $"https://maven.minecraftforge.net/net/minecraftforge/forge/{gameVersion}-{loaderVersion}/" +
        $"forge-{gameVersion}-{loaderVersion}-installer.jar";

    public static string ForgeInstallerMirrorUrl(string gameVersion, string loaderVersion) =>
        $"https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/{gameVersion}-{loaderVersion}/" +
        $"forge-{gameVersion}-{loaderVersion}-installer.jar";

    public static string NeoForgeInstallerUrl(string loaderVersion) =>
        $"https://maven.neoforged.net/releases/net/neoforged/neoforge/{loaderVersion}/" +
        $"neoforge-{loaderVersion}-installer.jar";

    public static IReadOnlyList<LoaderVersionInfo> ParseFabricVersions(string json)
    {
        using var document = JsonDocument.Parse(json);
        var result = new List<LoaderVersionInfo>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("loader", out var loader) ||
                !loader.TryGetProperty("version", out var version) ||
                string.IsNullOrWhiteSpace(version.GetString()))
            {
                continue;
            }

            var stable = loader.TryGetProperty("stable", out var stableElement) && stableElement.GetBoolean();
            result.Add(new LoaderVersionInfo(version.GetString()!, stable, ReleasedAt: null));
        }

        return result;
    }

    public static IReadOnlyList<LoaderVersionInfo> ParseForgeVersions(
        string xml, string gameVersion, string? recommended)
    {
        var prefix = gameVersion + "-";
        var versions = ParseMavenVersions(xml)
            .Where(v => v.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .Select(v => v[prefix.Length..])
            .Where(v => v.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(v => v, Comparer<string>.Create(CompareVersions))
            .Select(v => new LoaderVersionInfo(
                v,
                Stable: string.Equals(v, recommended, StringComparison.OrdinalIgnoreCase),
                ReleasedAt: null))
            .ToArray();

        return versions;
    }

    public static IReadOnlyList<LoaderVersionInfo> ParseNeoForgeVersions(string xml, string gameVersion)
    {
        var prefix = MapNeoForgePrefix(gameVersion);
        if (prefix is null)
        {
            return [];
        }

        var versions = ParseMavenVersions(xml)
            .Where(v => v.StartsWith(prefix + ".", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(v => v, Comparer<string>.Create(CompareVersions))
            .Select(v => new LoaderVersionInfo(
                v,
                Stable: !v.Contains("beta", StringComparison.OrdinalIgnoreCase),
                ReleasedAt: null))
            .ToArray();

        return versions;
    }

    /// <summary>
    /// NeoForge 版本号与游戏版本映射：1.21.1 → 21.1，1.21 → 21.0，1.20.4 → 20.4。
    /// 1.20.1 及更早没有 NeoForge，返回 null。
    /// </summary>
    public static string? MapNeoForgePrefix(string gameVersion)
    {
        var parts = gameVersion.Split('.');
        if (parts.Length < 2 || parts[0] != "1" || !int.TryParse(parts[1], out var minor) || minor < 20)
        {
            return null;
        }

        if (!int.TryParse(parts.Length >= 3 ? parts[2] : "0", out var patch))
        {
            return null;
        }

        // NeoForge 从 1.20.2 开始，1.20.1 及更早没有对应版本。
        if (minor == 20 && patch < 2)
        {
            return null;
        }

        return $"{minor}.{patch}";
    }

    public static int CompareVersions(string? left, string? right)
    {
        left ??= string.Empty;
        right ??= string.Empty;

        var leftTokens = Tokenize(left);
        var rightTokens = Tokenize(right);
        var count = Math.Min(leftTokens.Count, rightTokens.Count);
        for (var i = 0; i < count; i++)
        {
            var (leftNumber, leftText) = leftTokens[i];
            var (rightNumber, rightText) = rightTokens[i];

            int comparison;
            if (leftNumber && rightNumber)
            {
                var leftValue = long.TryParse(leftText, out var l) ? l : 0;
                var rightValue = long.TryParse(rightText, out var r) ? r : 0;
                comparison = leftValue.CompareTo(rightValue);
            }
            else if (leftNumber != rightNumber)
            {
                // 数字段优先于文字段（1.0.1 > 1.0-beta）。
                comparison = leftNumber ? 1 : -1;
            }
            else
            {
                comparison = string.Compare(leftText, rightText, StringComparison.OrdinalIgnoreCase);
            }

            if (comparison != 0)
            {
                return comparison;
            }
        }

        return leftTokens.Count.CompareTo(rightTokens.Count);
    }

    private async Task<string?> TryGetForgeRecommendedAsync(string gameVersion, CancellationToken ct)
    {
        try
        {
            var json = await GetStringAsync(ForgePromotionsUrl, ct);
            using var document = JsonDocument.Parse(json);
            return document.RootElement.TryGetProperty("promos", out var promos) &&
                   promos.TryGetProperty($"{gameVersion}-recommended", out var recommended)
                ? recommended.GetString()
                : null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested &&
                                   ex is HttpRequestException or JsonException or DownloadException)
        {
            return null;
        }
    }

    private async Task<string> GetStringAsync(string url, CancellationToken ct)
    {
        UrlGuard.Validate(url);
        try
        {
            return await _http.GetStringAsync(url, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            throw new InstallException("E3001", $"加载器元数据请求失败：{url}", ex);
        }
    }

    private static IReadOnlyList<string> ParseMavenVersions(string xml)
    {
        var document = XDocument.Parse(xml);
        return document.Root?
            .Element("versioning")?
            .Element("versions")?
            .Elements("version")
            .Select(e => e.Value.Trim())
            .Where(v => v.Length > 0)
            .ToArray() ?? [];
    }

    private static List<(bool IsNumber, string Text)> Tokenize(string value)
    {
        var tokens = new List<(bool, string)>();
        var index = 0;
        while (index < value.Length)
        {
            var start = index;
            var isNumber = char.IsDigit(value[index]);
            while (index < value.Length && char.IsDigit(value[index]) == isNumber &&
                   char.IsLetterOrDigit(value[index]))
            {
                index++;
            }

            if (start == index)
            {
                index++;
                continue;
            }

            tokens.Add((isNumber, value[start..index]));
        }

        return tokens;
    }

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen/0.1 (+https://github.com/limpidus-xiaoche/Lumen)");
        return client;
    }
}
