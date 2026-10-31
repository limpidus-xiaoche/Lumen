using System.Text.Json;
using System.Text.Json.Serialization;
using Launcher.Core.Download;
using Launcher.Core.Launch;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.Core.Java;

/// <summary>
/// Mojang Java 运行时安装器：按版本 JSON 的 <c>javaVersion.component</c> 下载官方 JRE，
/// 落地到 <c>shared/runtimes/&lt;component&gt;</c>，后续由 <see cref="JavaLocator"/> 自动发现。
/// </summary>
/// <remarks>
/// 数据来源：Mojang <c>java-runtime</c> 产品清单
/// <c>https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json</c>
/// （固定入口，版本哈希已写死在 URL 中）。该清单按平台/组件索引到各运行时 manifest。
/// 运行时 manifest 的每个文件同时提供 <c>raw</c> 与 <c>lzma</c> 两种下载条目；
/// 本项目只取 <c>raw</c>：无需引入 lzma 解压依赖，所有文件仍逐个做 SHA1 校验。
/// </remarks>
public sealed class JavaRuntimeInstaller
{
    private const string IndexUrl =
        "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private const string Platform = "windows-x64";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        AllowTrailingCommas = true,
    };

    private readonly LauncherPaths _paths;
    private readonly DownloadEngine _downloads;
    private readonly ILogger<JavaRuntimeInstaller> _logger;

    public JavaRuntimeInstaller(
        LauncherPaths paths,
        DownloadEngine downloads,
        ILogger<JavaRuntimeInstaller>? logger = null)
    {
        _paths = paths;
        _downloads = downloads;
        _logger = logger ?? NullLogger<JavaRuntimeInstaller>.Instance;
    }

    /// <summary>
    /// 优先使用版本 JSON 声明的组件；缺失时按 Java 大版本映射到 Mojang 组件名。
    /// </summary>
    public static string SelectComponent(string? component, int requiredMajor)
    {
        if (!string.IsNullOrWhiteSpace(component))
        {
            return component.Trim();
        }

        return requiredMajor switch
        {
            8 => "jre-legacy",
            16 => "java-runtime-alpha",
            17 => "java-runtime-beta",
            21 => "java-runtime-gamma",
            _ => throw new LaunchException(
                "E4002", $"没有与 Java {requiredMajor} 对应的 Mojang 运行时组件"),
        };
    }

    /// <summary>
    /// 确保运行时可用：目录已完整且版本满足要求时直接返回，否则下载并校验。
    /// </summary>
    public async Task<JavaRuntime> EnsureAsync(
        string? component,
        int requiredMajor,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        string componentId;
        try
        {
            componentId = PathGuard.ValidateId(
                SelectComponent(component, requiredMajor), nameof(component));
        }
        catch (ArgumentException ex)
        {
            throw new LaunchException("E4002", $"非法的 Java 运行时组件名：{component}", ex);
        }

        var runtimesRoot = Path.Combine(_paths.SharedDir, "runtimes");
        var target = Path.Combine(runtimesRoot, componentId);
        var javaExe = Path.Combine(target, "bin", "java.exe");

        if (File.Exists(javaExe) &&
            JavaVersionParser.TryRead(javaExe, out var cached) &&
            cached.MajorVersion >= requiredMajor)
        {
            _logger.LogDebug("Java 运行时已缓存：{Component} ({Path})", componentId, cached.Path);
            return cached;
        }

        progress?.Report($"正在准备 Java 运行时 {componentId}…");

        try
        {
            var manifest = await DownloadManifestAsync(componentId, ct);
            await DownloadFilesAsync(componentId, target, manifest, progress, ct);

            if (!File.Exists(javaExe) || !JavaVersionParser.TryRead(javaExe, out var runtime))
            {
                throw new LaunchException(
                    "E4001", $"Java 运行时 {componentId} 安装后未找到可用的 bin/java.exe");
            }

            if (runtime.MajorVersion < requiredMajor)
            {
                throw new LaunchException(
                    "E4001",
                    $"Java 运行时 {componentId} 版本过低：{runtime.MajorVersion} < {requiredMajor}");
            }

            _logger.LogInformation(
                "Java 运行时安装完成：{Component} {Version}", componentId, runtime.VersionString);
            return runtime;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (LaunchException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new LaunchException("E4001", $"Java 运行时 {componentId} 安装失败：{ex.Message}", ex);
        }
    }

    private async Task<RuntimeFileManifest> DownloadManifestAsync(string componentId, CancellationToken ct)
    {
        var indexPath = Path.Combine(_paths.CacheDir, "java-runtimes", "all.json");
        await _downloads.DownloadAsync(
            new DownloadItem(IndexUrl, indexPath, Sha1: null, Size: 0), null, ct);

        RuntimeIndex index;
        try
        {
            index = JsonSerializer.Deserialize<RuntimeIndex>(File.ReadAllText(indexPath), JsonOptions)
                ?? throw new LaunchException("E4002", "Mojang java-runtime 清单为空");
        }
        catch (JsonException ex)
        {
            throw new LaunchException("E4002", "Mojang java-runtime 清单解析失败", ex);
        }

        if (index.WindowsX64 is null ||
            !index.WindowsX64.TryGetValue(componentId, out var entries) ||
            entries.Count == 0)
        {
            throw new LaunchException("E4002", $"Mojang 清单中没有 {Platform} 组件 {componentId}");
        }

        var manifestRef = entries[0].Manifest;
        if (manifestRef?.Url is null || string.IsNullOrWhiteSpace(manifestRef.Sha1))
        {
            throw new LaunchException("E4002", $"组件 {componentId} 的运行时 manifest 缺少 url/sha1");
        }

        var manifestPath = Path.Combine(
            _paths.CacheDir, "java-runtimes", $"{componentId}.json");
        await _downloads.DownloadAsync(
            new DownloadItem(
                manifestRef.Url, manifestPath, manifestRef.Sha1, manifestRef.Size, DownloadKind.Other),
            null, ct);

        RuntimeFileManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<RuntimeFileManifest>(
                File.ReadAllText(manifestPath), JsonOptions);
        }
        catch (JsonException ex)
        {
            throw new LaunchException("E4002", $"Java 运行时 manifest 解析失败：{componentId}", ex);
        }

        return manifest ?? throw new LaunchException("E4002", $"Java 运行时 manifest 为空：{componentId}");
    }

    private async Task DownloadFilesAsync(
        string componentId,
        string target,
        RuntimeFileManifest manifest,
        IProgress<string>? progress,
        CancellationToken ct)
    {
        if (manifest.Files is null || manifest.Files.Count == 0)
        {
            throw new LaunchException("E4002", $"Java 运行时 manifest 没有 files：{componentId}");
        }

        var total = manifest.Files.Count(
            pair => string.Equals(pair.Value.Type, "file", StringComparison.OrdinalIgnoreCase));
        var done = 0;

        foreach (var (relative, entry) in manifest.Files)
        {
            ct.ThrowIfCancellationRequested();

            var dest = ResolveTarget(target, relative);
            if (string.Equals(entry.Type, "directory", StringComparison.OrdinalIgnoreCase))
            {
                Directory.CreateDirectory(dest);
                continue;
            }

            if (!string.Equals(entry.Type, "file", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogDebug(
                    "跳过 Java 运行时条目（类型 {Type}）：{Component}/{Relative}",
                    entry.Type, componentId, relative);
                continue;
            }

            var artifact = entry.Downloads?.Raw;
            if (artifact?.Url is null || string.IsNullOrWhiteSpace(artifact.Sha1))
            {
                throw new LaunchException(
                    "E4002", $"Java 运行时文件缺少 raw 下载信息或 SHA1：{componentId}/{relative}");
            }

            done++;
            progress?.Report($"正在下载 Java 运行时（{done}/{total}）…");
            await _downloads.DownloadAsync(
                new DownloadItem(
                    artifact.Url, dest, artifact.Sha1, artifact.Size, DownloadKind.Other),
                null, ct);
        }
    }

    private static string ResolveTarget(string target, string relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            throw new LaunchException("E4002", "Java 运行时 manifest 含空路径条目");
        }

        var candidate = Path.Combine(target, relative.Replace('/', Path.DirectorySeparatorChar));
        try
        {
            return PathGuard.EnsureWithin(target, candidate);
        }
        catch (InvalidOperationException ex)
        {
            throw new LaunchException(
                "E4002", $"Java 运行时 manifest 条目越界：{relative}", ex);
        }
    }

    private sealed class RuntimeIndex
    {
        [JsonPropertyName(Platform)]
        public Dictionary<string, List<RuntimeIndexEntry>>? WindowsX64 { get; set; }
    }

    private sealed class RuntimeIndexEntry
    {
        [JsonPropertyName("manifest")] public RuntimeManifestRef? Manifest { get; set; }
    }

    private sealed class RuntimeManifestRef
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("sha1")] public string? Sha1 { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }

    private sealed class RuntimeFileManifest
    {
        [JsonPropertyName("files")] public Dictionary<string, RuntimeFileEntry>? Files { get; set; }
    }

    private sealed class RuntimeFileEntry
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("downloads")] public RuntimeDownloads? Downloads { get; set; }
    }

    private sealed class RuntimeDownloads
    {
        [JsonPropertyName("raw")] public RuntimeArtifact? Raw { get; set; }
    }

    private sealed class RuntimeArtifact
    {
        [JsonPropertyName("url")] public string? Url { get; set; }
        [JsonPropertyName("sha1")] public string? Sha1 { get; set; }
        [JsonPropertyName("size")] public long Size { get; set; }
    }
}
