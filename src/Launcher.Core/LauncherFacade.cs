using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Core.Auth;
using Launcher.Core.Install;
using Launcher.Core.Java;
using Launcher.Core.Launch;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;

namespace Launcher.Core;

public sealed class LauncherFacade : ILauncherFacade
{
    private readonly LauncherPaths _paths;
    private readonly AppSettingsStore _settingsStore;
    private readonly SqliteStore _sqlite;
    private readonly ILogger<LauncherFacade> _logger;
    private readonly ManifestClient? _manifestClient;
    private readonly JavaLocator? _javaLocator;
    private readonly VanillaInstaller? _installer;
    private readonly LaunchService? _launcher;
    private readonly TaskRegistry _tasks;
    private readonly ModrinthClient _modrinth;
    private readonly CurseForgeClient _curseForge;
    private readonly LoaderMetaClient? _loaderMeta;

    public LauncherFacade(
        LauncherPaths paths,
        AppSettingsStore settingsStore,
        SqliteStore sqlite,
        ILogger<LauncherFacade> logger,
        ManifestClient? manifestClient = null,
        JavaLocator? javaLocator = null,
        VanillaInstaller? installer = null,
        TaskRegistry? tasks = null,
        LaunchService? launcher = null,
        LoaderMetaClient? loaderMeta = null)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _sqlite = sqlite;
        _logger = logger;
        _manifestClient = manifestClient;
        _javaLocator = javaLocator;
        _installer = installer;
        _tasks = tasks ?? new TaskRegistry();
        _launcher = launcher;
        _loaderMeta = loaderMeta;
        _modrinth = new ModrinthClient(paths.CacheDir);
        _curseForge = new CurseForgeClient(paths.CacheDir);
    }

    public async Task<LauncherSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var settings = _settingsStore.Load();
        var instances = await _sqlite.GetInstancesAsync(ct);
        var javas = _javaLocator?.Detect() ?? Array.Empty<JavaRuntime>();
        return new LauncherSnapshot(
            settings,
            instances,
            Array.Empty<AccountSummary>(),
            ActiveAccountId: null,
            javas,
            ManifestUpdatedAt: null,
            AppVersion: typeof(LauncherFacade).Assembly.GetName().Version?.ToString() ?? "0.0.0");
    }

    public async Task<VersionManifest> RefreshManifestAsync(ManifestRefreshMode mode, CancellationToken ct)
    {
        if (_manifestClient is not null)
        {
            return await _manifestClient.GetManifestAsync(mode == ManifestRefreshMode.Force, ct);
        }

        _logger.LogInformation("manifest 客户端未注入（M0 配置）：mode={Mode}", mode);
        return new VersionManifest(new LatestVersions("1.21.1", null), Array.Empty<VersionSummary>());
    }

    public Task<IReadOnlyList<LoaderVersionInfo>> GetLoaderVersionsAsync(
        LoaderKind loader, string gameVersion, CancellationToken ct) =>
        _loaderMeta is null
            ? Task.FromResult<IReadOnlyList<LoaderVersionInfo>>(Array.Empty<LoaderVersionInfo>())
            : _loaderMeta.GetVersionsAsync(loader, gameVersion, ct);

    public async Task<InstanceDetail> GetInstanceAsync(string instanceId, CancellationToken ct)
    {
        var instances = await _sqlite.GetInstancesAsync(ct);
        var summary = instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new KeyNotFoundException($"实例不存在：{instanceId}");

        return new InstanceDetail(
            summary,
            Path.Combine(_paths.InstancesDir, instanceId),
            JavaPath: null,
            MemoryMb: _settingsStore.Load().MaxMemoryMb,
            ExtraJvmArgs: string.Empty,
            ModCount: 0,
            DiskUsageBytes: 0);
    }

    public async Task<IReadOnlyList<TaskInfo>> GetTasksAsync(CancellationToken ct)
    {
        await _tasks.RestoreAsync(ct);
        return _tasks.Snapshot();
    }

    public Task CancelTaskAsync(string taskId, CancellationToken ct)
    {
        if (!_tasks.Cancel(taskId))
        {
            throw new KeyNotFoundException($"任务不存在：{taskId}");
        }

        return Task.CompletedTask;
    }

    public Task RetryTaskAsync(string taskId, CancellationToken ct)
    {
        if (!_tasks.Retry(taskId))
        {
            throw new KeyNotFoundException($"任务不存在或不可重试：{taskId}");
        }

        return Task.CompletedTask;
    }

    public async Task<InstanceSummary> InstallAsync(
        InstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        if (_installer is null)
        {
            throw new NotSupportedException("安装器未注入。");
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var taskId = _tasks.Start(
            TaskKind.Install,
            request.InstanceId,
            cts,
            token => RunInstallAsync(request, new Progress<InstallProgress>(), token));

        var throttled = new ThrottledProgress(
            progress,
            p => _tasks.Report(taskId, p.Percent, p.Message));

        try
        {
            var result = await RunInstallAsync(request, throttled, cts.Token);
            _tasks.Complete(taskId, TaskState.Succeeded, message: "安装完成");
            return result;
        }
        catch (OperationCanceledException)
        {
            _tasks.Complete(taskId, TaskState.Cancelled, message: "已取消");
            throw;
        }
        catch (Exception ex)
        {
            _tasks.Complete(taskId, TaskState.Failed, "E3003", ex.Message);
            throw;
        }
        finally
        {
            cts.Dispose();
        }
    }

    private Task<InstanceSummary> RunInstallAsync(
        InstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        _installer!.InstallAsync(request, progress, ct);

    public Task UpdateInstanceAsync(
        string instanceId, UpdateInstanceRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M2 未实现实例更新。");

    public async Task RepairInstanceAsync(
        string instanceId, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        if (_installer is null)
        {
            throw new NotSupportedException("安装器未注入。");
        }

        var instances = await _sqlite.GetInstancesAsync(ct);
        var instance = instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new KeyNotFoundException($"实例不存在：{instanceId}");

        await _sqlite.UpdateInstanceStateAsync(instanceId, InstanceState.Repairing, ct);
        var request = new InstallRequest(
            instance.Id, instance.Name, instance.VersionId,
            instance.Loader, instance.LoaderVersion, instance.Isolation,
            InstallMode.Reinstall);
        await InstallAsync(request, progress, ct);
    }

    public Task<InstanceSummary> InstallModpackAsync(
        ModpackInstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现整合包安装。");

    public async Task<GameSession> LaunchAsync(
        string instanceId, IProgress<LaunchEvent> events, CancellationToken ct)
    {
        if (_launcher is null)
        {
            throw new NotSupportedException("启动服务未注入。");
        }

        return await _launcher.LaunchAsync(instanceId, username: null, events, ct);
    }

    public Task StopGameAsync(string instanceId, StopMode mode, CancellationToken ct)
    {
        if (_launcher is null)
        {
            throw new NotSupportedException("启动服务未注入。");
        }

        return _launcher.StopGameAsync(instanceId, mode, ct);
    }

    public Task<IReadOnlyList<RunningSession>> GetRunningSessionsAsync(CancellationToken ct)
    {
        if (_launcher is null)
        {
            return Task.FromResult<IReadOnlyList<RunningSession>>([]);
        }

        return _launcher.GetRunningSessionsAsync(ct);
    }

    public Task<AccountSummary> LoginAsync(
        LoginRequest request, IProgress<LoginEvent> progress, CancellationToken ct)
    {
        if (request.Mode != AuthMode.Offline)
        {
            throw new NotSupportedException("M0 仅支持离线账户。");
        }

        var identity = OfflineAuth.Create(request.Username ?? "Player");
        progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"离线账户：{identity.Username}"));
        return Task.FromResult(new AccountSummary(
            identity.Uuid,
            AuthMode.Offline,
            identity.Username,
            identity.Uuid,
            AvatarUrl: null,
            IsActive: true,
            ExpiresAt: null));
    }

    public Task LogoutAsync(string accountId, CancellationToken ct) => Task.CompletedTask;

    public Task SetActiveAccountAsync(string accountId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<ModEntry>> ScanModsAsync(string instanceId, CancellationToken ct)
    {
        var dir = ModsDir(instanceId);
        if (!Directory.Exists(dir))
        {
            return Task.FromResult<IReadOnlyList<ModEntry>>(Array.Empty<ModEntry>());
        }

        var mods = new List<ModEntry>();
        foreach (var file in Directory.EnumerateFiles(dir, "*.jar*"))
        {
            var fileName = Path.GetFileName(file);
            var enabled = !fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
            var baseName = enabled ? fileName : fileName[..^".disabled".Length];

            var meta = ReadModMeta(file);
            var modId = meta.Id ?? baseName;
            var name = meta.Name ?? Path.GetFileNameWithoutExtension(baseName);

            mods.Add(new ModEntry(
                Id: modId,
                Name: name,
                Version: meta.Version,
                FileName: fileName,
                Enabled: enabled,
                Loader: LoaderKind.Vanilla,
                Dependencies: Array.Empty<string>(),
                IconPath: TryExtractIcon(baseName, file, meta.IconEntry)));
        }

        return Task.FromResult<IReadOnlyList<ModEntry>>(mods);
    }

    /// <summary>把外部 jar 复制到实例的 mods 目录。</summary>
    public Task ImportModAsync(string instanceId, string sourceJarPath, CancellationToken ct)
    {
        var dir = ModsDir(instanceId);
        Directory.CreateDirectory(dir);
        File.Copy(sourceJarPath, Path.Combine(dir, Path.GetFileName(sourceJarPath)), overwrite: true);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OnlineModSummary>> SearchOnlineModsAsync(
        ModSource source, string query, string? modCategory, LoaderKind? loader, string? gameVersion, int page, CancellationToken ct) =>
        source == ModSource.CurseForge
            ? _curseForge.SearchAsync(query, loader, gameVersion, page, ct)
            : _modrinth.SearchAsync(query, modCategory, ModrinthClient.LoaderName(loader), gameVersion, page, ct);

    public Task<string> InstallOnlineModAsync(
        string instanceId, ModSource source, string projectId, string? gameVersion, LoaderKind loader,
        IProgress<InstallProgress> progress, CancellationToken ct) =>
        source == ModSource.CurseForge
            ? _curseForge.InstallAsync(ModsDir(instanceId), projectId, gameVersion, loader, progress, ct)
            : _modrinth.InstallAsync(
                ModsDir(instanceId), projectId, gameVersion, ModrinthClient.LoaderName(loader), progress, ct);

    /// <summary>读取 Fabric 元数据（id / 名称 / 版本 / 图标入口），非 Fabric 模组返回全空。</summary>
    private static (string? Id, string? Name, string? Version, string? IconEntry) ReadModMeta(string jarPath)
    {
        try
        {
            using var zip = ZipFile.OpenRead(jarPath);
            var entry = zip.GetEntry("fabric.mod.json");
            if (entry is null)
            {
                return (null, null, null, null);
            }

            using var stream = entry.Open();
            using var doc = JsonDocument.Parse(stream);
            var root = doc.RootElement;

            var id = root.TryGetProperty("id", out var i) ? i.GetString() : null;
            var name = root.TryGetProperty("name", out var n) ? n.GetString() : null;
            var version = root.TryGetProperty("version", out var v) ? v.GetString() : null;

            string? icon = null;
            if (root.TryGetProperty("icon", out var iconElement))
            {
                icon = iconElement.ValueKind == JsonValueKind.String
                    ? iconElement.GetString()
                    : iconElement.ValueKind == JsonValueKind.Object && iconElement.TryGetProperty("64", out var i64)
                        ? i64.GetString()
                        : null;
            }

            return (id, name, version, icon);
        }
        catch
        {
            return (null, null, null, null);
        }
    }

    /// <summary>从 jar 中提取图标到缓存目录，供界面显示。</summary>
    private string? TryExtractIcon(string baseFileName, string jarPath, string? iconEntry)
    {
        try
        {
            using var zip = ZipFile.OpenRead(jarPath);
            var entry = (iconEntry is null ? null : zip.GetEntry(iconEntry.Replace('\\', '/')))
                        ?? zip.GetEntry("icon.png")
                        ?? zip.GetEntry("logo.png")
                        ?? zip.GetEntry("pack.png");

            if (entry is null)
            {
                return null;
            }

            var dir = Path.Combine(_paths.CacheDir, "mod-icons");
            Directory.CreateDirectory(dir);
            var hash = Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(baseFileName)))[..16];
            var target = Path.Combine(dir, hash + ".png");

            entry.ExtractToFile(target, overwrite: true);
            return target;
        }
        catch
        {
            return null;
        }
    }

    public Task SetModEnabledAsync(string instanceId, string modId, bool enabled, CancellationToken ct)
    {
        var path = FindModFile(instanceId, modId)
            ?? throw new FileNotFoundException($"模组不存在：{modId}");

        var currentlyEnabled = !path.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase);
        if (currentlyEnabled == enabled)
        {
            return Task.CompletedTask;
        }

        var target = enabled ? path[..^".disabled".Length] : path + ".disabled";
        File.Move(path, target);
        return Task.CompletedTask;
    }

    public Task DeleteModAsync(string instanceId, string modId, CancellationToken ct)
    {
        var path = FindModFile(instanceId, modId)
            ?? throw new FileNotFoundException($"模组不存在：{modId}");

        File.Delete(path);
        return Task.CompletedTask;
    }

    private string ModsDir(string instanceId) => Path.Combine(_paths.InstancesDir, instanceId, "mods");

    /// <summary>按模组 Id（Fabric id 或文件名）定位已安装的 jar。</summary>
    private string? FindModFile(string instanceId, string modId)
    {
        var dir = ModsDir(instanceId);
        if (!Directory.Exists(dir))
        {
            return null;
        }

        foreach (var file in Directory.EnumerateFiles(dir, "*.jar*"))
        {
            var fileName = Path.GetFileName(file);
            var baseName = fileName.EndsWith(".disabled", StringComparison.OrdinalIgnoreCase)
                ? fileName[..^".disabled".Length]
                : fileName;

            if (string.Equals(baseName, modId, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }

            if (string.Equals(ReadModMeta(file).Id, modId, StringComparison.OrdinalIgnoreCase))
            {
                return file;
            }
        }

        return null;
    }

    public async Task DeleteInstanceAsync(string instanceId, bool keepSaves, CancellationToken ct)
    {
        await Task.CompletedTask;
        throw new NotSupportedException("M0 未实现实例删除。");
    }

    public Task<IReadOnlyList<InstanceSummary>> ImportExternalAsync(
        string path, IProgress<ImportProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现外部导入。");

    public Task UpdateSettingsAsync(AppSettingsPatch patch, CancellationToken ct)
    {
        var current = _settingsStore.Load();
        var updated = current with
        {
            MaxMemoryMb = patch.MaxMemoryMb ?? current.MaxMemoryMb,
            GameDir = patch.GameDir ?? current.GameDir,
            JavaPath = patch.JavaPath ?? current.JavaPath,
            ProxyMode = patch.ProxyMode ?? current.ProxyMode,
            ProxyAddress = patch.ProxyAddress ?? current.ProxyAddress,
            DownloadSource = patch.DownloadSource ?? current.DownloadSource,
            MaxThreads = patch.MaxThreads ?? current.MaxThreads,
            AutoUpdate = patch.AutoUpdate ?? current.AutoUpdate,
            AccentColor = patch.AccentColor ?? current.AccentColor,
        };
        _settingsStore.Save(updated);
        return Task.CompletedTask;
    }

    public Task<UpdateInfo?> CheckUpdateAsync(CancellationToken ct) =>
        Task.FromResult<UpdateInfo?>(null);

    public Task ApplyUpdateAsync(UpdateInfo info, IProgress<UpdateProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现自动更新。");

    public Task<IReadOnlyList<LogFileInfo>> GetLogFilesAsync(string? instanceId, CancellationToken ct)
    {
        if (!Directory.Exists(_paths.LogsDir))
        {
            return Task.FromResult<IReadOnlyList<LogFileInfo>>(Array.Empty<LogFileInfo>());
        }

        var files = new DirectoryInfo(_paths.LogsDir)
            .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Select(f => new LogFileInfo(f.FullName, null, LogKind.Launcher, f.Length,
                new DateTimeOffset(f.LastWriteTimeUtc)))
            .ToArray();

        return Task.FromResult<IReadOnlyList<LogFileInfo>>(files);
    }

    public Task<string> ExportDiagnosticsAsync(string? instanceId, CancellationToken ct)
    {
        Directory.CreateDirectory(_paths.ExportsDir);
        var output = Path.Combine(
            _paths.ExportsDir,
            $"diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip");

        using var archive = ZipFile.Open(output, ZipArchiveMode.Create);
        AddEntryIfExists(archive, _paths.ConfigFile, "launcher.json");
        foreach (var log in Directory.Exists(_paths.LogsDir)
                     ? Directory.GetFiles(_paths.LogsDir, "*.log")
                     : Array.Empty<string>())
        {
            AddEntryIfExists(archive, log, $"logs/{Path.GetFileName(log)}");
        }

        archive.CreateEntry("README.txt");
        return Task.FromResult(output);
    }

    private static void AddEntryIfExists(ZipArchive archive, string path, string entryName)
    {
        if (!File.Exists(path))
        {
            return;
        }

        archive.CreateEntryFromFile(path, entryName, CompressionLevel.Optimal);
    }
}
