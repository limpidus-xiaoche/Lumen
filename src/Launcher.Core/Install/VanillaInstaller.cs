using System.IO.Compression;
using System.Text.Json;
using Launcher.Core.Download;
using Launcher.Core.Game;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;

namespace Launcher.Core.Install;

public sealed class VanillaInstaller
{
    private static readonly InstallStage[] StageOrder =
    [
        InstallStage.VersionJson,
        InstallStage.ClientJar,
        InstallStage.Libraries,
        InstallStage.Natives,
        InstallStage.Assets,
        InstallStage.Finalizing,
    ];

    private static readonly double[] StageWeights = [1, 5, 15, 1, 20, 1];

    private readonly LauncherPaths _paths;
    private readonly VersionResolver _resolver;
    private readonly DownloadEngine _downloads;
    private readonly SqliteStore _sqlite;
    private readonly ILogger<VanillaInstaller> _logger;

    public VanillaInstaller(
        LauncherPaths paths,
        VersionResolver resolver,
        DownloadEngine downloads,
        SqliteStore sqlite,
        ILogger<VanillaInstaller> logger)
    {
        _paths = paths;
        _resolver = resolver;
        _downloads = downloads;
        _sqlite = sqlite;
        _logger = logger;
    }

    public async Task<InstanceSummary> InstallAsync(
        InstallRequest request,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var instanceId = ResolveInstanceId(request);
        var gameDir = request.Isolation == IsolationLevel.None
            ? Path.Combine(_paths.SharedDir, "game")
            : Path.Combine(_paths.InstancesDir, instanceId);

        Directory.CreateDirectory(gameDir);
        foreach (var sub in new[] { "mods", "config", "saves", "resourcepacks", "shaderpacks", "logs", "crash-reports" })
        {
            Directory.CreateDirectory(Path.Combine(gameDir, sub));
        }

        GameOptions.EnsureDefaultLanguage(gameDir);

        await _sqlite.UpsertInstanceAsync(
            instanceId, request.DisplayName, gameDir, request.VersionId,
            request.Loader, request.LoaderVersion, request.Isolation,
            InstanceState.Installing, ct: ct);

        var versionDir = Path.Combine(_paths.SharedDir, "versions", request.VersionId);
        Directory.CreateDirectory(versionDir);

        try
        {
            await JournalAsync(instanceId, InstallStage.VersionJson, "start", request.VersionId, ct);
            Report(progress, InstallStage.VersionJson, 20, "解析版本 JSON");
            var detail = await _resolver.ResolveAsync(request.VersionId, ct);
            var mergedJson = JsonSerializer.Serialize(detail, new JsonSerializerOptions { WriteIndented = true });
            AtomicFile.WriteAllText(Path.Combine(versionDir, $"{request.VersionId}.json"), mergedJson);
            Report(progress, InstallStage.VersionJson, 100, "版本 JSON 已保存");
            await JournalAsync(instanceId, InstallStage.VersionJson, "done", request.VersionId, ct);

            var client = detail.Downloads?.Client;
            if (client?.Url is not null)
            {
                await JournalAsync(instanceId, InstallStage.ClientJar, "start", client.Url, ct);
                var jarPath = Path.Combine(versionDir, $"{request.VersionId}.jar");
                await _downloads.DownloadAsync(
                    new DownloadItem(client.Url, jarPath, client.Sha1, client.Size, DownloadKind.ClientJar),
                    MapProgress(progress, InstallStage.ClientJar, client.Size),
                    ct);
                Report(progress, InstallStage.ClientJar, 100, "客户端 jar 完成");
                await JournalAsync(instanceId, InstallStage.ClientJar, "done", jarPath, ct);
            }

            var nativeJars = new List<(string JarPath, string Classifier)>();
            var libraries = BuildLibraryItems(detail, nativeJars);
            await JournalAsync(instanceId, InstallStage.Libraries, "start", $"{libraries.Count} 个库", ct);
            await DownloadManyAsync(libraries, InstallStage.Libraries, progress, ct);
            Report(progress, InstallStage.Libraries, 100, "依赖库完成");
            await JournalAsync(instanceId, InstallStage.Libraries, "done", string.Empty, ct);

            await JournalAsync(instanceId, InstallStage.Natives, "start", $"{nativeJars.Count} 个 native", ct);
            await ExtractNativesAsync(request.VersionId, nativeJars, ct);
            Report(progress, InstallStage.Natives, 100, "native 解压完成");
            await JournalAsync(instanceId, InstallStage.Natives, "done", string.Empty, ct);

            if (request.SkipAssets)
            {
                Report(progress, InstallStage.Assets, 100, "已跳过资源文件（--skip-assets）");
                await JournalAsync(instanceId, InstallStage.Assets, "skipped", string.Empty, ct);
            }
            else
            {
                await InstallAssetsAsync(detail, progress, ct);
                await JournalAsync(instanceId, InstallStage.Assets, "done", string.Empty, ct);
            }

            await JournalAsync(instanceId, InstallStage.Finalizing, "start", string.Empty, ct);
            Report(progress, InstallStage.Finalizing, 100, "完成");
            await _sqlite.UpdateInstanceStateAsync(instanceId, InstanceState.Ready, ct);
            await JournalAsync(instanceId, InstallStage.Finalizing, "done", string.Empty, ct);
            Report(progress, InstallStage.Done, 100, "安装完成");

            return new InstanceSummary(
                instanceId, request.DisplayName, request.VersionId,
                request.Loader, request.LoaderVersion, request.Isolation,
                InstanceState.Ready, null, null);
        }
        catch (Exception)
        {
            await _sqlite.UpdateInstanceStateAsync(instanceId, InstanceState.Broken, CancellationToken.None);
            await JournalAsync(instanceId, InstallStage.Finalizing, "failed", "安装失败", CancellationToken.None);
            throw;
        }
    }

    private List<DownloadItem> BuildLibraryItems(
        VersionDetail detail,
        List<(string JarPath, string Classifier)> nativeJars)
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
            if (artifact?.Url is not null)
            {
                var relative = artifact.Path ?? MavenPath(library.Name);
                var dest = PathGuard.EnsureWithin(_paths.SharedDir, Path.Combine(_paths.SharedDir, "libraries", relative));
                items.Add(new DownloadItem(artifact.Url, dest, artifact.Sha1, artifact.Size, DownloadKind.Library));
            }
            else if (artifact is null && library.Downloads is null && !string.IsNullOrWhiteSpace(library.Name))
            {
                var relative = MavenPath(library.Name);
                var dest = PathGuard.EnsureWithin(_paths.SharedDir, Path.Combine(_paths.SharedDir, "libraries", relative));
                items.Add(new DownloadItem(
                    $"https://libraries.minecraft.net/{relative.Replace('\\', '/')}",
                    dest, null, 0, DownloadKind.Library));
            }

            var classifier = NativeClassifier(library);
            if (classifier is null || library.Downloads?.Classifiers is null ||
                !library.Downloads.Classifiers.TryGetValue(classifier, out var nativeArtifact) ||
                nativeArtifact.Url is null)
            {
                continue;
            }

            var nativeRelative = nativeArtifact.Path ?? MavenPath(library.Name, classifier);
            var nativeDest = PathGuard.EnsureWithin(
                _paths.SharedDir, Path.Combine(_paths.SharedDir, "libraries", nativeRelative));
            items.Add(new DownloadItem(
                nativeArtifact.Url, nativeDest, nativeArtifact.Sha1, nativeArtifact.Size, DownloadKind.Native));
            nativeJars.Add((nativeDest, classifier));
        }

        return items;
    }

    private async Task DownloadManyAsync(
        IReadOnlyList<DownloadItem> items,
        InstallStage stage,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        if (items.Count == 0)
        {
            return;
        }

        var total = items.Count;
        var done = 0;
        Report(progress, stage, 0, $"下载 {total} 个文件");

        await Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct },
            async (item, token) =>
            {
                await _downloads.DownloadAsync(item, null, token);
                var completed = Interlocked.Increment(ref done);
                Report(progress, stage, completed * 100d / total, $"{completed}/{total}", item.DestPath);
            });
    }

    private async Task InstallAssetsAsync(
        VersionDetail detail,
        IProgress<InstallProgress> progress,
        CancellationToken ct)
    {
        var indexRef = detail.AssetIndex;
        if (indexRef?.Url is null)
        {
            Report(progress, InstallStage.Assets, 100, "无资源索引，跳过");
            return;
        }

        var indexDir = Path.Combine(_paths.SharedDir, "assets", "indexes");
        Directory.CreateDirectory(indexDir);
        var indexPath = Path.Combine(indexDir, $"{indexRef.Id}.json");
        await _downloads.DownloadAsync(
            new DownloadItem(indexRef.Url, indexPath, indexRef.Sha1, indexRef.Size, DownloadKind.Asset),
            null, ct);

        var index = JsonSerializer.Deserialize<AssetIndexJson>(File.ReadAllText(indexPath))
            ?? throw new DownloadException("E2003", "资源索引解析失败");

        var objectsDir = Path.Combine(_paths.SharedDir, "assets", "objects");
        var items = new List<DownloadItem>();
        var seenHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        long totalBytes = 0;
        foreach (var (_, entry) in index.Objects)
        {
            if (!seenHashes.Add(entry.Hash))
            {
                continue;
            }

            var prefix = entry.Hash[..2];
            var dest = Path.Combine(objectsDir, prefix, entry.Hash);
            items.Add(new DownloadItem(
                $"https://resources.download.minecraft.net/{prefix}/{entry.Hash}",
                dest, entry.Hash, entry.Size, DownloadKind.Asset));
            totalBytes += entry.Size;
        }

        CheckDiskSpace(items.Sum(i => i.Size));

        long downloaded = 0;
        Report(progress, InstallStage.Assets, 0, $"下载 {items.Count} 个资源文件");
        await Parallel.ForEachAsync(
            items,
            new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct },
            async (item, token) =>
            {
                await _downloads.DownloadAsync(item, null, token);
                var done = Interlocked.Add(ref downloaded, item.Size);
                Report(progress, InstallStage.Assets, totalBytes > 0 ? done * 100d / totalBytes : 100,
                    $"{FormatSize(done)} / {FormatSize(totalBytes)}");
            });

        if (index.Virtual || index.MapToResources)
        {
            var virtualRoot = Path.Combine(_paths.SharedDir, "assets", "virtual", "legacy");
            foreach (var (name, entry) in index.Objects)
            {
                var source = Path.Combine(objectsDir, entry.Hash[..2], entry.Hash);
                var target = PathGuard.EnsureWithin(virtualRoot, Path.Combine(virtualRoot, name.Replace('/', Path.DirectorySeparatorChar)));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: true);
            }
        }
    }

    private async Task ExtractNativesAsync(
        string versionId,
        IReadOnlyList<(string JarPath, string Classifier)> nativeJars,
        CancellationToken ct)
    {
        var nativesRoot = Path.Combine(_paths.SharedDir, "natives", versionId, "x86_64");
        Directory.CreateDirectory(nativesRoot);

        foreach (var (jarPath, _) in nativeJars)
        {
            if (!File.Exists(jarPath))
            {
                continue;
            }

            await using var stream = File.OpenRead(jarPath);
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
            foreach (var entry in archive.Entries)
            {
                if (entry.FullName.EndsWith('/') ||
                    !entry.Name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
                    entry.FullName.StartsWith("META-INF/", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var target = PathGuard.EnsureWithin(nativesRoot, Path.Combine(nativesRoot, entry.Name));
                entry.ExtractToFile(target, overwrite: true);
            }
        }

        await Task.CompletedTask;
    }

    private void CheckDiskSpace(long requiredBytes)
    {
        if (requiredBytes <= 0)
        {
            return;
        }

        var root = Path.GetPathRoot(_paths.LocalRoot);
        if (string.IsNullOrEmpty(root))
        {
            return;
        }

        var available = new DriveInfo(root).AvailableFreeSpace;
        var required = (long)(requiredBytes * 1.2) + 512L * 1024 * 1024;
        if (required > available)
        {
            throw new DownloadException("E2004", $"磁盘空间不足：需要 {FormatSize(required)}，可用 {FormatSize(available)}");
        }
    }

    private async Task JournalAsync(
        string instanceId, InstallStage stage, string status, string detail, CancellationToken ct)
    {
        await _sqlite.RecordJournalAsync(instanceId, stage.ToString(), status, detail, ct);
        _logger.LogInformation("install {Instance} {Stage} {Status}: {Detail}", instanceId, stage, status, detail);
    }

    private static void Report(
        IProgress<InstallProgress> progress,
        InstallStage stage,
        double stagePercent,
        string message,
        string? currentFile = null)
    {
        var index = Array.IndexOf(StageOrder, stage);
        var prefix = 0d;
        for (var i = 0; i < index; i++)
        {
            prefix += StageWeights[i];
        }

        var total = StageWeights.Sum();
        var global = stage == InstallStage.Done
            ? 100
            : (prefix + StageWeights[index] * stagePercent / 100) / total * 100;

        progress.Report(new InstallProgress(
            stage, global, 0, 0, 0, currentFile, message));
    }

    private static IProgress<DownloadProgress> MapProgress(
        IProgress<InstallProgress> progress, InstallStage stage, long size) =>
        new Progress<DownloadProgress>(p =>
        {
            var percent = size > 0 ? p.BytesDownloaded * 100d / size : 100;
            Report(progress, stage, percent, $"{FormatSize(p.BytesDownloaded)} / {FormatSize(size)}",
                Path.GetFileName(p.CurrentFile));
        });

    private static string? NativeClassifier(Library library)
    {
        if (library.Natives is null || !library.Natives.TryGetValue("windows", out var template))
        {
            return null;
        }

        return template.Replace("${arch}", "64");
    }

    public static string MavenPath(string name, string? classifierOverride = null)
    {
        var parts = name.Split(':');
        if (parts.Length < 3)
        {
            return name;
        }

        var group = parts[0].Replace('.', '/');
        var artifact = parts[1];
        var version = parts[2];
        var classifier = classifierOverride ?? (parts.Length >= 4 ? parts[3] : null);
        var fileName = classifier is null
            ? $"{artifact}-{version}.jar"
            : $"{artifact}-{version}-{classifier}.jar";
        return Path.Combine(group, artifact, version, fileName);
    }

    public static string ResolveInstanceId(InstallRequest request)
    {
        var id = request.InstanceId ?? Slug(request.DisplayName);
        return PathGuard.ValidateId(id, nameof(request.InstanceId));
    }

    private static string Slug(string name)
    {
        var slug = new string(name.Select(c =>
            char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '-').ToArray()).Trim('-');
        if (string.IsNullOrWhiteSpace(slug))
        {
            var fallback = $"instance-{Guid.NewGuid():N}";
            return fallback[..17];
        }

        return slug[..Math.Min(slug.Length, 32)];
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024 / 1024:F1} GB",
        >= 1024L * 1024 => $"{bytes / 1024d / 1024:F1} MB",
        >= 1024 => $"{bytes / 1024d:F1} KB",
        _ => $"{bytes} B",
    };
}
