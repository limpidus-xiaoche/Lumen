using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Protocol;

namespace Launcher.Core.Meta;

/// <summary>CurseForge 官方 API 客户端（需要 API Key：环境变量 LUMEN_CURSEFORGE_KEY）。</summary>
public sealed class CurseForgeClient
{
    private const string BaseUrl = "https://api.curseforge.com/v1";
    private const int ModsClassId = 6;
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(30) };

    private readonly string _cacheDir;
    private readonly string? _apiKey;

    public CurseForgeClient(string cacheDir)
    {
        _cacheDir = cacheDir;
        _apiKey = Environment.GetEnvironmentVariable("LUMEN_CURSEFORGE_KEY");
    }

    /// <summary>是否配置了 API Key；未配置时页面会提示而不是静默失败。</summary>
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    /// <summary>加载器枚举 → CurseForge 的 modLoaderType。</summary>
    public static int? LoaderType(LoaderKind? loader) => loader switch
    {
        LoaderKind.Forge => 1,
        LoaderKind.Fabric => 4,
        LoaderKind.NeoForge => 6,
        _ => null,
    };

    public async Task<IReadOnlyList<OnlineModSummary>> SearchAsync(
        string query, LoaderKind? loader, string? gameVersion, int page, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "未配置 CurseForge API Key，请设置环境变量 LUMEN_CURSEFORGE_KEY 后重启启动器");
        }

        var url = $"{BaseUrl}/mods/search?gameId=432&classId={ModsClassId}&pageSize=20" +
                  $"&index={Math.Max(0, page - 1) * 20}" +
                  $"&sortField=2&sortOrder=desc&searchFilter={Uri.EscapeDataString(query)}";

        if (!string.IsNullOrEmpty(gameVersion))
        {
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
        }

        if (LoaderType(loader) is { } loaderType)
        {
            url += $"&modLoaderType={loaderType}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _apiKey);

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var results = new List<OnlineModSummary>();

        foreach (var item in document.RootElement.GetProperty("data").EnumerateArray())
        {
            var id = item.GetProperty("id").GetInt32().ToString();
            var name = item.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? id : id;
            var summary = item.TryGetProperty("summary", out var summaryElement) ? summaryElement.GetString() ?? string.Empty : string.Empty;
            var downloads = item.TryGetProperty("downloadCount", out var countElement) ? countElement.GetInt64() : 0;
            var author = item.TryGetProperty("authors", out var authorsElement) &&
                         authorsElement.ValueKind == JsonValueKind.Array &&
                         authorsElement.GetArrayLength() > 0
                ? authorsElement[0].GetProperty("name").GetString()
                : null;

            string? iconUrl = null;
            if (item.TryGetProperty("logo", out var logo) && logo.ValueKind == JsonValueKind.Object)
            {
                iconUrl = logo.TryGetProperty("thumbnailUrl", out var thumb) ? thumb.GetString() : null;
            }

            results.Add(new OnlineModSummary(
                id, name, name, summary, author, downloads,
                await CacheIconAsync(iconUrl, ct)));
        }

        return results;
    }

    public async Task<string> InstallAsync(
        string modsDir, string projectId, string? gameVersion, LoaderKind? loader,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException(
                "未配置 CurseForge API Key，请设置环境变量 LUMEN_CURSEFORGE_KEY 后重启启动器");
        }

        var url = $"{BaseUrl}/mods/{projectId}/files?pageSize=20";
        if (!string.IsNullOrEmpty(gameVersion))
        {
            url += $"&gameVersion={Uri.EscapeDataString(gameVersion)}";
        }

        if (LoaderType(loader) is { } loaderType)
        {
            url += $"&modLoaderType={loaderType}";
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", _apiKey);

        using var response = await Http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var files = document.RootElement.GetProperty("data").EnumerateArray().ToArray();
        if (files.Length == 0)
        {
            throw new InvalidOperationException("没有匹配该游戏版本与加载器的模组文件");
        }

        // 部分作者关闭了第三方下载，downloadUrl 为 null，需要提示用户
        var file = files.FirstOrDefault(f =>
            f.TryGetProperty("downloadUrl", out var download) && download.ValueKind == JsonValueKind.String);
        if (file.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidOperationException("该作者未开放第三方下载，请前往 CurseForge 页面手动下载");
        }

        var fileUrl = file.GetProperty("downloadUrl").GetString()!;
        var fileName = file.GetProperty("fileName").GetString() ?? Path.GetFileName(new Uri(fileUrl).LocalPath);

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
            var target = Path.Combine(dir, "cf-" + hash + ".png");

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
