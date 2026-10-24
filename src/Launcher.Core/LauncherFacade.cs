using System.IO.Compression;
using Launcher.Core.Accounts;
using Launcher.Core.Auth;
using Launcher.Core.Install;
using Launcher.Core.Instances;
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
    private readonly AccountStore? _accounts;
    private readonly MicrosoftAuth? _microsoft;
    private readonly AuthlibAuth? _authlib;
    private readonly LoaderCatalog? _loaderCatalog;
    private readonly ModpackInstaller? _modpackInstaller;

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
        AccountStore? accounts = null,
        MicrosoftAuth? microsoft = null,
        AuthlibAuth? authlib = null,
        LoaderCatalog? loaderCatalog = null,
        ModpackInstaller? modpackInstaller = null)
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
        _accounts = accounts;
        _microsoft = microsoft;
        _authlib = authlib;
        _loaderCatalog = loaderCatalog;
        _modpackInstaller = modpackInstaller;
    }

    public async Task<LauncherSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        var settings = _settingsStore.Load();
        var instances = await _sqlite.GetInstancesAsync(ct);
        var javas = _javaLocator?.Detect() ?? Array.Empty<JavaRuntime>();
        return new LauncherSnapshot(
            settings,
            instances,
            _accounts?.GetSummaries() ?? Array.Empty<AccountSummary>(),
            _accounts?.ActiveId,
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
        LoaderKind loader, string gameVersion, CancellationToken ct)
    {
        if (_loaderCatalog is null)
        {
            return Task.FromResult<IReadOnlyList<LoaderVersionInfo>>(Array.Empty<LoaderVersionInfo>());
        }

        return _loaderCatalog.GetVersionsAsync(loader, gameVersion, ct);
    }

    public async Task<InstanceDetail> GetInstanceAsync(string instanceId, CancellationToken ct)
    {
        var instances = await _sqlite.GetInstancesAsync(ct);
        var summary = instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new KeyNotFoundException($"实例不存在：{instanceId}");

        var gameDir = await ResolveGameDirAsync(summary, ct);
        return new InstanceDetail(
            summary,
            gameDir,
            JavaPath: null,
            MemoryMb: _settingsStore.Load().MaxMemoryMb,
            ExtraJvmArgs: string.Empty,
            ModCount: ModScanner.Scan(gameDir, summary.Loader).Count,
            DiskUsageBytes: 0);
    }

    public Task<IReadOnlyList<TaskInfo>> GetTasksAsync(CancellationToken ct) =>
        Task.FromResult(_tasks.Snapshot());

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

    public async Task UpdateInstanceAsync(
        string instanceId, UpdateInstanceRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var instances = await _sqlite.GetInstancesAsync(ct);
        var current = instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new KeyNotFoundException($"实例不存在：{instanceId}");

        if (request.MemoryMb is { } memoryMb)
        {
            var settings = _settingsStore.Load();
            _settingsStore.Save(settings with { MaxMemoryMb = memoryMb });
        }

        var install = new InstallRequest(
            instanceId,
            request.DisplayName ?? current.Name,
            request.VersionId ?? current.VersionId,
            request.Loader ?? current.Loader,
            request.LoaderVersion ?? current.LoaderVersion,
            request.Isolation ?? current.Isolation,
            InstallMode.ChangeLoader);

        await InstallAsync(install, progress, ct);
    }

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
        ModpackInstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        if (_modpackInstaller is null)
        {
            throw new NotSupportedException("整合包安装器未注入。");
        }

        return _modpackInstaller.InstallAsync(request, progress, ct);
    }

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

    public async Task<AccountSummary> LoginAsync(
        LoginRequest request, IProgress<LoginEvent> progress, CancellationToken ct)
    {
        if (_accounts is null)
        {
            throw new NotSupportedException("账户存储未注入。");
        }

        StoredAccount account;
        switch (request.Mode)
        {
            case AuthMode.Offline:
            {
                var identity = OfflineAuth.Create(
                    string.IsNullOrWhiteSpace(request.Username) ? "Player" : request.Username.Trim());
                account = new StoredAccount(
                    $"offline:{identity.Uuid}", AuthMode.Offline, identity.Username, identity.Uuid,
                    AvatarUrl: null, identity.AccessToken, RefreshToken: null, ExpiresAt: null);
                progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"离线账户：{identity.Username}"));
                break;
            }

            case AuthMode.Microsoft:
            {
                if (_microsoft is null)
                {
                    throw new NotSupportedException("Microsoft 登录未注入。");
                }

                var result = await _microsoft.LoginAsync(progress, ct);
                account = new StoredAccount(
                    $"msa:{result.Uuid}", AuthMode.Microsoft, result.Username, result.Uuid,
                    result.AvatarUrl, result.AccessToken, result.RefreshToken, result.ExpiresAt);
                progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"Microsoft 账户：{result.Username}"));
                break;
            }

            case AuthMode.Authlib:
            {
                if (_authlib is null)
                {
                    throw new NotSupportedException("外置登录未注入。");
                }

                if (string.IsNullOrWhiteSpace(request.ServerUrl) ||
                    string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    throw new AuthException("E6005", "外置登录需要服务器地址、用户名与密码。");
                }

                var result = await _authlib.LoginAsync(
                    request.ServerUrl, request.Username, request.Password, ct);
                account = new StoredAccount(
                    $"authlib:{result.Uuid}@{new Uri(result.ServerUrl).Host}", AuthMode.Authlib,
                    result.Username, result.Uuid, AvatarUrl: null, result.AccessToken,
                    RefreshToken: null, ExpiresAt: null, result.ServerUrl, result.ClientToken);
                progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"外置账户：{result.Username}"));
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "未知的登录方式。");
        }

        _accounts.Upsert(account);
        _accounts.SetActive(account.Id);
        _logger.LogInformation("账户已登录：{Type} {Username}", account.Type, account.Username);
        return ToSummary(account, isActive: true);
    }

    public Task LogoutAsync(string accountId, CancellationToken ct)
    {
        if (_accounts is null)
        {
            return Task.CompletedTask;
        }

        _accounts.Remove(accountId);
        _logger.LogInformation("账户已移除：{AccountId}", accountId);
        return Task.CompletedTask;
    }

    public Task SetActiveAccountAsync(string accountId, CancellationToken ct)
    {
        _accounts?.SetActive(accountId);
        return Task.CompletedTask;
    }

    private static AccountSummary ToSummary(StoredAccount account, bool isActive) =>
        new(account.Id, account.Type, account.Username, account.Uuid, account.AvatarUrl, isActive, account.ExpiresAt);

    public async Task<IReadOnlyList<ModEntry>> ScanModsAsync(string instanceId, CancellationToken ct)
    {
        var (instance, gameDir) = await FindInstanceAsync(instanceId, ct);
        return ModScanner.Scan(gameDir, instance.Loader);
    }

    public async Task SetModEnabledAsync(
        string instanceId, string modId, bool enabled, CancellationToken ct)
    {
        var (_, gameDir) = await FindInstanceAsync(instanceId, ct);
        if (!ModScanner.SetEnabled(gameDir, modId, enabled))
        {
            throw new KeyNotFoundException($"模组不存在：{modId}");
        }
    }

    public async Task DeleteInstanceAsync(string instanceId, bool keepSaves, CancellationToken ct)
    {
        var (instance, gameDir) = await FindInstanceAsync(instanceId, ct);

        if (_launcher is not null)
        {
            var running = await _launcher.GetRunningSessionsAsync(ct);
            if (running.Any(r => string.Equals(r.InstanceId, instanceId, StringComparison.Ordinal)))
            {
                throw new InvalidOperationException("实例正在运行，请先停止游戏再删除。");
            }
        }

        if (keepSaves && Directory.Exists(gameDir))
        {
            var exportRoot = Path.Combine(
                _paths.ExportsDir, $"{instanceId}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}");
            foreach (var sub in new[] { "saves", "screenshots" })
            {
                var source = Path.Combine(gameDir, sub);
                if (!Directory.Exists(source))
                {
                    continue;
                }

                var target = Path.Combine(exportRoot, sub);
                Directory.CreateDirectory(exportRoot);
                try
                {
                    Directory.Move(source, target);
                }
                catch (IOException)
                {
                    CopyDirectory(source, target);
                    Directory.Delete(source, recursive: true);
                }
            }
        }

        // 只删除由本启动器管理的实例目录；外部接管与 shared/game 一律不动。
        if (IsManagedInstanceDir(gameDir) && Directory.Exists(gameDir))
        {
            Directory.Delete(gameDir, recursive: true);
        }

        await _sqlite.DeleteInstanceAsync(instance.Id, ct);
        _logger.LogInformation("实例已删除：{Instance}（keepSaves={KeepSaves}）", instanceId, keepSaves);
    }

    public async Task<IReadOnlyList<InstanceSummary>> ImportExternalAsync(
        string path, IProgress<ImportProgress> progress, CancellationToken ct)
    {
        var candidate = ExternalImporter.Detect(path);
        progress.Report(new ImportProgress("detect", 0, 0, 0, candidate.SourceKind));
        var instanceId = await ExternalImporter.CopyAsync(
            candidate, _paths, candidate.Name, progress, ct);

        var name = string.IsNullOrWhiteSpace(candidate.Name) ? instanceId : candidate.Name;
        await _sqlite.UpsertInstanceAsync(
            instanceId, name, Path.Combine(_paths.InstancesDir, instanceId),
            candidate.GameVersion, candidate.Loader, candidate.LoaderVersion,
            IsolationLevel.ModsConfig, InstanceState.Ready, ct: ct);

        _logger.LogInformation("外部实例已导入：{Source} -> {Instance}", candidate.SourceKind, instanceId);
        return
        [
            new InstanceSummary(
                instanceId, name, candidate.GameVersion, candidate.Loader, candidate.LoaderVersion,
                IsolationLevel.ModsConfig, InstanceState.Ready, null, null),
        ];
    }

    private async Task<(InstanceSummary Instance, string GameDir)> FindInstanceAsync(
        string instanceId, CancellationToken ct)
    {
        var instances = await _sqlite.GetInstancesAsync(ct);
        var instance = instances.FirstOrDefault(i => i.Id == instanceId)
            ?? throw new KeyNotFoundException($"实例不存在：{instanceId}");
        return (instance, await ResolveGameDirAsync(instance, ct));
    }

    private async Task<string> ResolveGameDirAsync(InstanceSummary instance, CancellationToken ct)
    {
        var root = await _sqlite.GetRootPathAsync(instance.Id, ct);
        if (!string.IsNullOrWhiteSpace(root))
        {
            return root;
        }

        return instance.Isolation == IsolationLevel.None
            ? Path.Combine(_paths.SharedDir, "game")
            : Path.Combine(_paths.InstancesDir, instance.Id);
    }

    private bool IsManagedInstanceDir(string path)
    {
        var root = Path.GetFullPath(_paths.InstancesDir)
            .TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        return full.StartsWith(root, StringComparison.OrdinalIgnoreCase);
    }

    private static void CopyDirectory(string source, string target)
    {
        Directory.CreateDirectory(target);
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

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
