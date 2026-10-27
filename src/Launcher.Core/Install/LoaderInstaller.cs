using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Launcher.Core.Download;
using Launcher.Core.Java;
using Launcher.Core.Launch;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;

namespace Launcher.Core.Install;

/// <summary>
/// 加载器安装：Fabric 走 profile JSON 合并；Forge/NeoForge 走官方 installer 的 --installClient。
/// </summary>
public sealed class LoaderInstaller
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    private readonly LauncherPaths _paths;
    private readonly DownloadEngine _downloads;
    private readonly VersionResolver _resolver;
    private readonly JavaLocator _javaLocator;
    private readonly AppSettingsStore _settings;
    private readonly ILogger<LoaderInstaller> _logger;
    private readonly HttpClient _http;

    public LoaderInstaller(
        LauncherPaths paths,
        DownloadEngine downloads,
        VersionResolver resolver,
        JavaLocator javaLocator,
        AppSettingsStore settings,
        ILogger<LoaderInstaller> logger,
        HttpClient? http = null)
    {
        _paths = paths;
        _downloads = downloads;
        _resolver = resolver;
        _javaLocator = javaLocator;
        _settings = settings;
        _logger = logger;
        _http = http ?? CreateHttpClient();
    }

    public async Task<string> InstallAsync(
        InstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        if (!LoaderIds.IsLoader(request.Loader))
        {
            return request.VersionId;
        }

        var loaderVersion = string.IsNullOrWhiteSpace(request.LoaderVersion)
            ? throw new InstallException("E3001", $"{request.Loader} 需要指定加载器版本")
            : request.LoaderVersion.Trim();
        var canonical = LoaderIds.EffectiveVersionId(request.VersionId, request.Loader, loaderVersion);

        switch (request.Loader)
        {
            case LoaderKind.Fabric:
                await InstallFabricAsync(request.VersionId, loaderVersion, canonical, progress, ct);
                break;
            case LoaderKind.Forge:
            case LoaderKind.NeoForge:
                await InstallWithInstallerAsync(request, loaderVersion, canonical, progress, ct);
                break;
            default:
                throw new InstallException("E3001", $"{request.Loader} 尚未支持");
        }

        _logger.LogInformation("加载器安装完成：{Loader} {Version} -> {Id}",
            request.Loader, loaderVersion, canonical);
        Report(progress, 100, $"加载器安装完成：{canonical}");
        return canonical;
    }

    private async Task InstallFabricAsync(
        string gameVersion,
        string loaderVersion,
        string canonical,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        Report(progress, 5, "获取 Fabric 配置");
        var json = await GetStringAsync(
            LoaderCatalog.FabricProfileUrlFor(gameVersion, loaderVersion), ct);

        var node = JsonNode.Parse(json) as JsonObject
            ?? throw new InstallException("E3001", "Fabric profile JSON 解析失败");
        node["id"] = canonical;
        NormalizeFabricLibraries(node);

        var jsonText = node.ToJsonString(IndentedJson);
        var dir = Path.Combine(_paths.SharedDir, "versions", canonical);
        Directory.CreateDirectory(dir);
        AtomicFile.WriteAllText(Path.Combine(dir, $"{canonical}.json"), jsonText);

        var detail = ManifestClient.DeserializeDetail(jsonText);
        await DownloadLibrariesAsync(detail, progress, ct);
    }

    private static void NormalizeFabricLibraries(JsonObject profile)
    {
        if (profile["libraries"] is not JsonArray libraries)
        {
            return;
        }

        foreach (var entry in libraries)
        {
            if (entry is not JsonObject library ||
                library["name"]?.GetValue<string>() is not { Length: > 0 } name)
            {
                continue;
            }

            var baseUrl = library["url"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(baseUrl))
            {
                continue;
            }

            var path = VanillaInstaller.MavenPath(name).Replace('\\', '/');
            library["downloads"] = new JsonObject
            {
                ["artifact"] = new JsonObject
                {
                    ["path"] = path,
                    ["sha1"] = library["sha1"]?.GetValue<string>(),
                    ["size"] = library["size"]?.GetValue<long>() ?? 0,
                    ["url"] = baseUrl.TrimEnd('/') + "/" + path,
                },
            };
        }
    }

    private async Task DownloadLibrariesAsync(
        VersionDetail detail, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var context = new RuleContext();
        var items = new List<DownloadItem>();
        foreach (var library in detail.Libraries)
        {
            if (!RuleEvaluator.IsAllowed(library.Rules, context))
            {
                continue;
            }

            var artifact = library.Downloads?.Artifact;
            if (artifact?.Url is null)
            {
                continue;
            }

            var relative = artifact.Path ?? VanillaInstaller.MavenPath(library.Name);
            var dest = PathGuard.EnsureWithin(
                _paths.SharedDir, Path.Combine(_paths.SharedDir, "libraries", relative));
            items.Add(new DownloadItem(artifact.Url, dest, artifact.Sha1, artifact.Size, DownloadKind.Library));
        }

        if (items.Count == 0)
        {
            Report(progress, 90, "加载器无额外依赖库");
            return;
        }

        var done = 0;
        Report(progress, 10, $"下载 {items.Count} 个加载器依赖");
        await Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = 8, CancellationToken = ct },
            async (item, token) =>
            {
                await _downloads.DownloadAsync(item, null, token);
                var completed = Interlocked.Increment(ref done);
                Report(progress, 10 + completed * 80d / items.Count, $"{completed}/{items.Count}");
            });
    }

    private async Task InstallWithInstallerAsync(
        InstallRequest request,
        string loaderVersion,
        string canonical,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var (url, mirrors) = request.Loader switch
        {
            LoaderKind.Forge => (
                LoaderCatalog.ForgeInstallerUrl(request.VersionId, loaderVersion),
                new[] { LoaderCatalog.ForgeInstallerMirrorUrl(request.VersionId, loaderVersion) }),
            LoaderKind.NeoForge => (LoaderCatalog.NeoForgeInstallerUrl(loaderVersion), Array.Empty<string>()),
            _ => throw new InstallException("E3001", $"{request.Loader} 尚未支持"),
        };

        var cacheDir = Path.Combine(_paths.CacheDir, "loaders");
        var jarPath = Path.Combine(cacheDir, Path.GetFileName(new Uri(url).AbsolutePath));
        var sha1 = await TryGetSha1Async(url, ct);

        Report(progress, 5, $"下载 {request.Loader} 安装器");
        await _downloads.DownloadAsync(
            new DownloadItem(url, jarPath, sha1, 0, DownloadKind.Loader, mirrors),
            null, ct);

        EnsureLauncherProfiles();
        var java = await SelectJavaAsync(request.VersionId, ct);
        Report(progress, 30, $"运行 {request.Loader} 安装器（{java.MajorVersion}）");
        await RunInstallerAsync(java.Path, jarPath, _paths.SharedDir, progress, ct);
        NormalizeInstalledVersion(request.VersionId, loaderVersion, canonical);
    }

    private async Task RunInstallerAsync(
        string javaPath,
        string installerJar,
        string targetDir,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = targetDir,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        startInfo.ArgumentList.Add("-jar");
        startInfo.ArgumentList.Add(installerJar);
        startInfo.ArgumentList.Add("--installClient");
        startInfo.ArgumentList.Add(targetDir);

        var tail = new Queue<string>();
        void OnLine(string? line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return;
            }

            lock (tail)
            {
                tail.Enqueue(line);
                while (tail.Count > 20)
                {
                    tail.Dequeue();
                }
            }

            Report(progress, 60, line.Length > 180 ? line[..180] : line);
        }

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.OutputDataReceived += (_, e) => OnLine(e.Data);
        process.ErrorDataReceived += (_, e) => OnLine(e.Data);

        try
        {
            if (!process.Start())
            {
                throw new InstallException("E3001", "无法启动加载器安装器");
            }
        }
        catch (Exception ex) when (ex is not InstallException)
        {
            throw new InstallException("E3001", $"启动加载器安装器失败：{ex.Message}", ex);
        }

        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(TimeSpan.FromMinutes(15));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            try
            {
                process.Kill(entireProcessTree: true);
            }
            catch (InvalidOperationException)
            {
                // 进程已退出。
            }

            throw;
        }

        if (process.ExitCode != 0)
        {
            string detail;
            lock (tail)
            {
                detail = string.Join(" | ", tail.TakeLast(5));
            }

            throw new InstallException(
                "E3001", $"加载器安装器失败（exit {process.ExitCode}）：{detail}");
        }
    }

    private void NormalizeInstalledVersion(
        string gameVersion,
        string loaderVersion,
        string canonical)
    {
        var canonicalDir = Path.Combine(_paths.SharedDir, "versions", canonical);
        var canonicalJson = Path.Combine(canonicalDir, $"{canonical}.json");
        if (File.Exists(canonicalJson))
        {
            return;
        }

        var versionsRoot = Path.Combine(_paths.SharedDir, "versions");
        if (!Directory.Exists(versionsRoot))
        {
            throw new InstallException("E3001", "加载器安装后缺少 versions 目录");
        }

        foreach (var dir in Directory.GetDirectories(versionsRoot))
        {
            var id = Path.GetFileName(dir);
            var jsonPath = Path.Combine(dir, $"{id}.json");
            if (!File.Exists(jsonPath) ||
                !id.Contains(loaderVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            VersionDetail detail;
            try
            {
                detail = ManifestClient.DeserializeDetail(File.ReadAllText(jsonPath));
            }
            catch (JsonException)
            {
                continue;
            }

            if (!string.Equals(detail.InheritsFrom, gameVersion, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Directory.Exists(canonicalDir))
            {
                Directory.Delete(canonicalDir, recursive: true);
            }

            Directory.Move(dir, canonicalDir);
            var targetJson = Path.Combine(canonicalDir, $"{canonical}.json");
            var json = File.ReadAllText(jsonPath);
            File.Delete(jsonPath);
            if (JsonNode.Parse(json) is JsonObject node)
            {
                node["id"] = canonical;
                json = node.ToJsonString(IndentedJson);
            }

            AtomicFile.WriteAllText(targetJson, json);
            return;
        }

        throw new InstallException(
            "E3001", $"加载器安装后未找到继承自 {gameVersion} 的版本 JSON（期望 {canonical}）");
    }

    private async Task<JavaRuntime> SelectJavaAsync(string gameVersion, CancellationToken ct)
    {
        var detail = await _resolver.ResolveAsync(gameVersion, ct);
        var settings = _settings.Load();
        var runtimes = _javaLocator.Detect().ToList();
        if (!string.IsNullOrWhiteSpace(settings.JavaPath) &&
            File.Exists(settings.JavaPath) &&
            JavaVersionParser.TryRead(settings.JavaPath, out var preferred) &&
            runtimes.All(r => !string.Equals(r.Path, preferred.Path, StringComparison.OrdinalIgnoreCase)))
        {
            runtimes.Add(preferred);
        }

        return JavaVersionPolicy.Select(detail, runtimes, settings.JavaPath);
    }

    private void EnsureLauncherProfiles()
    {
        var path = Path.Combine(_paths.SharedDir, "launcher_profiles.json");
        if (File.Exists(path))
        {
            return;
        }

        AtomicFile.WriteAllText(path, """{"profiles":{},"settings":{},"version":3}""");
    }

    private async Task<string?> TryGetSha1Async(string url, CancellationToken ct)
    {
        try
        {
            UrlGuard.Validate(url + ".sha1");
            var text = await _http.GetStringAsync(url + ".sha1", ct);
            var sha1 = text.Trim();
            return sha1.Length == 40 && sha1.All(Uri.IsHexDigit) ? sha1.ToLowerInvariant() : null;
        }
        catch (Exception ex) when (!ct.IsCancellationRequested && ex is HttpRequestException or DownloadException)
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
            throw new InstallException("E3001", $"加载器配置请求失败：{url}", ex);
        }
    }

    private static void Report(IProgress<InstallProgress> progress, double percent, string message) =>
        progress.Report(new InstallProgress(
            InstallStage.Loader, percent, 0, 0, 0, CurrentFile: null, message));

    private static HttpClient CreateHttpClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen/0.1 (+https://github.com/limpidus-xiaoche/Lumen)");
        return client;
    }
}
