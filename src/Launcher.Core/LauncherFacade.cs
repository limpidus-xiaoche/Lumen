using System.IO.Compression;
using Launcher.Core.Auth;
using Launcher.Core.Java;
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

    public LauncherFacade(
        LauncherPaths paths,
        AppSettingsStore settingsStore,
        SqliteStore sqlite,
        ILogger<LauncherFacade> logger,
        ManifestClient? manifestClient = null,
        JavaLocator? javaLocator = null)
    {
        _paths = paths;
        _settingsStore = settingsStore;
        _sqlite = sqlite;
        _logger = logger;
        _manifestClient = manifestClient;
        _javaLocator = javaLocator;
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
        Task.FromResult<IReadOnlyList<LoaderVersionInfo>>(Array.Empty<LoaderVersionInfo>());

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

    public Task<IReadOnlyList<TaskInfo>> GetTasksAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskInfo>>(Array.Empty<TaskInfo>());

    public Task CancelTaskAsync(string taskId, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现任务取消。");

    public Task RetryTaskAsync(string taskId, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现任务重试。");

    public Task<InstanceSummary> InstallAsync(
        InstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现安装。");

    public Task UpdateInstanceAsync(
        string instanceId, UpdateInstanceRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现实例更新。");

    public Task RepairInstanceAsync(
        string instanceId, IProgress<InstallProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现实例修复。");

    public Task<InstanceSummary> InstallModpackAsync(
        ModpackInstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现整合包安装。");

    public Task<GameSession> LaunchAsync(
        string instanceId, IProgress<LaunchEvent> events, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现游戏启动。");

    public Task StopGameAsync(string instanceId, StopMode mode, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现游戏结束。");

    public Task<IReadOnlyList<RunningSession>> GetRunningSessionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RunningSession>>(Array.Empty<RunningSession>());

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

    public Task<IReadOnlyList<ModEntry>> ScanModsAsync(string instanceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ModEntry>>(Array.Empty<ModEntry>());

    public Task SetModEnabledAsync(string instanceId, string modId, bool enabled, CancellationToken ct) =>
        throw new NotSupportedException("M0 未实现模组启停。");

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
