using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Protocol;

namespace Launcher.Core.Meta;

/// <summary>Modrinth 开放 API 客户端：模组搜索、按版本/加载器挑选文件、下载安装、图标缓存。</summary>
public sealed class ModrinthClient
{
    private const string BaseUrl = "https://api.modrinth.com/v2";
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _cacheDir;

    public ModrinthClient(string cacheDir)
    {
        _cacheDir = cacheDir;
    }

    /// <summary>加载器枚举 → Modrinth 的 loader 名称。</summary>
    public static string? LoaderName(LoaderKind? loader) => loader switch
    {
        LoaderKind.Fabric => "fabric",
        LoaderKind.Forge => "forge",
        LoaderKind.NeoForge => "neoforge",
        _ => null,
    };

    public async Task<IReadOnlyList<OnlineModSummary>> SearchAsync(
        string query, string? modCategory, string? loaderName, string? gameVersion, int page, CancellationToken ct)
    {
        // 空关键字视为「浏览热门」：按下载量排序，打开页面即自动加载
        var index = string.IsNullOrWhiteSpace(query) ? "downloads" : "relevance";
        var offset = Math.Max(0, page - 1) * 20;
        var url = $"{BaseUrl}/search?limit=20&offset={offset}&index={index}&query={Uri.EscapeDataString(query)}";

        var facets = new List<string>();
        if (!string.IsNullOrEmpty(modCategory))
        {
            facets.Add($"[\"categories:{modCategory}\"]");
        }

        if (!string.IsNullOrEmpty(loaderName))
        {
            facets.Add($"[\"categories:{loaderName}\"]");
        }

        if (!string.IsNullOrEmpty(gameVersion))
        {
            facets.Add($"[\"versions:{gameVersion}\"]");
        }

        if (facets.Count > 0)
        {
            url += "&facets=" + Uri.EscapeDataString($"[{string.Join(",", facets)}]");
        }

        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var results = new List<OnlineModSummary>();

        foreach (var hit in document.RootElement.GetProperty("hits").EnumerateArray())
        {
            var projectId = hit.GetProperty("project_id").GetString() ?? string.Empty;
            var slug = hit.TryGetProperty("slug", out var slugElement) ? slugElement.GetString() ?? projectId : projectId;
            var title = hit.TryGetProperty("title", out var titleElement) ? titleElement.GetString() ?? slug : slug;
            var description = hit.TryGetProperty("description", out var descElement) ? descElement.GetString() ?? string.Empty : string.Empty;
            var author = hit.TryGetProperty("author", out var authorElement) ? authorElement.GetString() : null;
            var downloads = hit.TryGetProperty("downloads", out var downloadsElement) ? downloadsElement.GetInt64() : 0;
            var iconUrl = hit.TryGetProperty("icon_url", out var iconElement) ? iconElement.GetString() : null;

            results.Add(new OnlineModSummary(
                projectId, slug, title, description, author, downloads,
                await CacheIconAsync(iconUrl, ct)));
        }

        return results;
    }

    /// <summary>下载指定项目匹配当前加载器/游戏版本的最新文件到 mods 目录，返回文件名。</summary>
    public async Task<string> InstallAsync(
        string modsDir, string projectId, string? gameVersion, string? loaderName,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var url = $"{BaseUrl}/project/{projectId}/version";
        var parameters = new List<string>();

        if (!string.IsNullOrEmpty(gameVersion))
        {
            parameters.Add($"game_versions={Uri.EscapeDataString($"[\"{gameVersion}\"]")}");
        }

        if (!string.IsNullOrEmpty(loaderName))
        {
            parameters.Add($"loaders={Uri.EscapeDataString($"[\"{loaderName}\"]")}");
        }

        if (parameters.Count > 0)
        {
            url += "?" + string.Join("&", parameters);
        }

        using var response = await Http.GetAsync(url, ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var versions = document.RootElement;
        if (versions.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("没有匹配该游戏版本与加载器的模组文件");
        }

        var files = versions[0].GetProperty("files").EnumerateArray().ToArray();
        var file = files.FirstOrDefault(f => f.TryGetProperty("primary", out var p) && p.GetBoolean());
        if (file.ValueKind == JsonValueKind.Undefined)
        {
            file = files[0];
        }

        var fileUrl = file.GetProperty("url").GetString()
                      ?? throw new InvalidOperationException("模组文件地址缺失");
        var fileName = file.GetProperty("filename").GetString()
                       ?? throw new InvalidOperationException("模组文件名缺失");

        Directory.CreateDirectory(modsDir);
        var target = Path.Combine(modsDir, fileName);

        progress.Report(new InstallProgress(
            InstallStage.Libraries, 0, 0, 0, 0, fileName, $"正在下载 {fileName}"));

        await using (var source = await Http.GetStreamAsync(fileUrl, ct))
        await using (var destination = File.Create(target))
        {
            await source.CopyToAsync(destination, ct);
        }

        progress.Report(new InstallProgress(
            InstallStage.Done, 100, 0, 0, 0, fileName, $"已安装 {fileName}"));

        return fileName;
    }

    private async Task<string?> CacheIconAsync(string? iconUrl, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(iconUrl))
        {
            return null;
        }

        try
        {
            var dir = Path.Combine(_cacheDir, "mod-icons");
            Directory.CreateDirectory(dir);
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(iconUrl)))[..16];
            var target = Path.Combine(dir, "online-" + hash + ".png");

            if (!File.Exists(target))
            {
                var bytes = await Http.GetByteArrayAsync(iconUrl, ct);
                await File.WriteAllBytesAsync(target, bytes, ct);
            }

            return target;
        }
        catch
        {
            return null;
        }
    }
}
