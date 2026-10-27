using Launcher.Core.Auth;
using Launcher.Protocol;

namespace Launcher.App.Mock;

/// <summary>
/// 内核未就绪时的假实现：保证 UI 可以独立推进（M0）。
/// </summary>
public sealed class FakeLauncherFacade : ILauncherFacade
{
    private readonly AppSettings _settings = new() { GameDir = @"C:\Lumen\game" };

    private readonly object _accountSync = new();
    private readonly List<AccountSummary> _accounts;
    private string? _activeAccountId;

    /// <param name="startSignedIn">为 false 时模拟全新安装（没有账户）。</param>
    public FakeLauncherFacade(bool startSignedIn = true)
    {
        _accounts = startSignedIn
            ?
            [
                new AccountSummary(
                    "offline-demo", AuthMode.Offline, "Player",
                    "00000000000030008000000000000000", null, true, null),
            ]
            : [];
        _activeAccountId = startSignedIn ? "offline-demo" : null;
    }

    private readonly object _instanceSync = new();
    private readonly List<InstanceSummary> _instances =
    [
        new("demo-vanilla", "原版 1.21.1", "1.21.1", LoaderKind.Vanilla, null,
            IsolationLevel.ModsConfig, InstanceState.Ready, null, DateTimeOffset.Now.AddDays(-1)),
        new("demo-fabric", "Fabric 1.20.1", "1.20.1", LoaderKind.Fabric, "0.16.0",
            IsolationLevel.ModsConfig, InstanceState.Ready, null, DateTimeOffset.Now.AddDays(-3)),
    ];

    public Task<LauncherSnapshot> GetSnapshotAsync(CancellationToken ct)
    {
        AccountSummary[] accounts;
        string? activeId;
        InstanceSummary[] instances;
        lock (_accountSync)
        {
            activeId = _activeAccountId;
            accounts = _accounts
                .Select(a => a with { IsActive = a.Id == activeId })
                .ToArray();
        }

        lock (_instanceSync)
        {
            instances = _instances.ToArray();
        }

        return Task.FromResult(new LauncherSnapshot(
            _settings,
            instances,
            accounts,
            activeId,
            [new JavaRuntime(@"C:\Java\jdk-21\bin\java.exe", 21, true, "Temurin", "21.0.8")],
            DateTimeOffset.Now.AddMinutes(-5),
            "0.1.0-m0"));
    }

    /// <summary>任务中心演示数据：覆盖进行中、不确定进度、失败与重启中断四种状态。</summary>
    private readonly List<TaskInfo> _tasks =
    [
        new("fake-task-run", "demo-fabric", TaskKind.Install, TaskState.Running, 42, "依赖库 · 32/78", 1, null),
        new("fake-task-unknown", "demo-vanilla", TaskKind.Install, TaskState.Running, -1, "正在解析版本清单", 1, null),
        new("fake-task-failed", "demo-vanilla", TaskKind.Repair, TaskState.Failed, 60, "下载失败：连接超时", 2, "E2001"),
        new("fake-task-interrupted", "demo-fabric", TaskKind.Install, TaskState.Failed, 20, "上次运行中断（可修复）", 1, TaskErrorCodes.Interrupted),
        new("fake-task-done", "demo-vanilla", TaskKind.Install, TaskState.Succeeded, 100, "安装完成", 1, null),
    ];

    public Task<VersionManifest> RefreshManifestAsync(ManifestRefreshMode mode, CancellationToken ct) =>
        Task.FromResult(new VersionManifest(new LatestVersions("1.21.1", "26.4-snapshot-5"),
        [
            new VersionSummary("1.21.1", "release", "https://example.invalid/1.21.1.json", null),
            new VersionSummary("1.20.1", "release", "https://example.invalid/1.20.1.json", null),
        ]));

    public Task<IReadOnlyList<LoaderVersionInfo>> GetLoaderVersionsAsync(
        LoaderKind loader, string gameVersion, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoaderVersionInfo>>(loader switch
        {
            LoaderKind.Fabric =>
            [
                new LoaderVersionInfo("0.16.0", true, DateTimeOffset.Now.AddDays(-30)),
                new LoaderVersionInfo("0.15.11", false, DateTimeOffset.Now.AddDays(-120)),
            ],
            LoaderKind.Forge =>
            [
                new LoaderVersionInfo("47.3.0", true, DateTimeOffset.Now.AddDays(-20)),
                new LoaderVersionInfo("47.2.20", false, DateTimeOffset.Now.AddDays(-90)),
            ],
            LoaderKind.NeoForge =>
            [
                new LoaderVersionInfo("21.1.72", true, DateTimeOffset.Now.AddDays(-10)),
                new LoaderVersionInfo("21.0.167", false, DateTimeOffset.Now.AddDays(-60)),
            ],
            _ => [],
        });

    public Task<InstanceDetail> GetInstanceAsync(string instanceId, CancellationToken ct)
    {
        InstanceSummary summary;
        lock (_instanceSync)
        {
            summary = _instances.First(i => i.Id == instanceId);
        }

        return Task.FromResult(new InstanceDetail(
            summary, Path.Combine(@"C:\Lumen\instances", instanceId), null, 4096, string.Empty, 12, 1_234_567));
    }

    public Task<IReadOnlyList<TaskInfo>> GetTasksAsync(CancellationToken ct)
    {
        lock (_tasks)
        {
            return Task.FromResult<IReadOnlyList<TaskInfo>>(_tasks.ToArray());
        }
    }

    public Task CancelTaskAsync(string taskId, CancellationToken ct)
    {
        lock (_tasks)
        {
            var index = _tasks.FindIndex(t => t.TaskId == taskId);
            if (index >= 0)
            {
                _tasks[index] = _tasks[index] with
                {
                    State = TaskState.Cancelled,
                    Message = "已取消",
                };
            }
        }

        return Task.CompletedTask;
    }

    public Task RetryTaskAsync(string taskId, CancellationToken ct)
    {
        lock (_tasks)
        {
            var index = _tasks.FindIndex(t => t.TaskId == taskId);
            if (index >= 0)
            {
                var source = _tasks[index];
                _tasks[index] = source with
                {
                    TaskId = Guid.NewGuid().ToString("N"),
                    State = TaskState.Running,
                    Percent = 0,
                    Message = "重新开始",
                    Attempts = source.Attempts + 1,
                    ErrorCode = null,
                };
            }
        }

        return Task.CompletedTask;
    }

    public async Task<InstanceSummary> InstallAsync(
        InstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var stages = new[]
        {
            InstallStage.VersionJson, InstallStage.ClientJar, InstallStage.Libraries,
            InstallStage.Natives, InstallStage.Assets, InstallStage.Loader, InstallStage.Finalizing,
        };

        // 契约约定：Fake 每 200ms 报一次假进度；前期阶段总量未知，Percent = -1（不确定态）
        var taskId = Guid.NewGuid().ToString("N");
        lock (_tasks)
        {
            _tasks.Insert(0, new TaskInfo(
                taskId, request.InstanceId, TaskKind.Install, TaskState.Running, -1, "准备安装", 1, null));
        }

        for (var i = 0; i < stages.Length; i++)
        {
            var percent = i < 2 ? -1 : (i + 1) * 100d / stages.Length;
            var message = $"模拟安装：{stages[i]}";
            progress.Report(new InstallProgress(
                stages[i], percent, 0, 0, 0, null, message));
            lock (_tasks)
            {
                var index = _tasks.FindIndex(t => t.TaskId == taskId);
                if (index >= 0)
                {
                    _tasks[index] = _tasks[index] with
                    {
                        State = TaskState.Running,
                        Percent = percent < 0 ? 0 : percent,
                        Message = message,
                    };
                }
            }

            await Task.Delay(200, ct);
        }

        progress.Report(new InstallProgress(InstallStage.Done, 100, 0, 0, 0, null, "模拟安装完成"));
        lock (_tasks)
        {
            var index = _tasks.FindIndex(t => t.TaskId == taskId);
            if (index >= 0)
            {
                _tasks[index] = _tasks[index] with
                {
                    State = TaskState.Succeeded,
                    Percent = 100,
                    Message = "模拟安装完成",
                };
            }
        }

        var result = new InstanceSummary(
            request.InstanceId ?? $"fake-{Guid.NewGuid():N}",
            request.DisplayName,
            request.VersionId,
            request.Loader,
            request.LoaderVersion,
            request.Isolation,
            InstanceState.Ready,
            null,
            DateTimeOffset.Now);
        lock (_instanceSync)
        {
            var index = _instances.FindIndex(i => i.Id == result.Id);
            if (index >= 0)
            {
                _instances[index] = result;
            }
            else
            {
                _instances.Insert(0, result);
            }
        }

        return result;
    }

    public async Task UpdateInstanceAsync(
        string instanceId, UpdateInstanceRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        progress.Report(new InstallProgress(
            InstallStage.Loader, -1, 0, 0, 0, null, request.Loader is null ? "保存设置" : "更换加载器"));
        await Task.Delay(150, ct);

        lock (_instanceSync)
        {
            var index = _instances.FindIndex(i => i.Id == instanceId);
            if (index < 0)
            {
                throw new KeyNotFoundException($"实例不存在：{instanceId}");
            }

            var current = _instances[index];
            _instances[index] = current with
            {
                Name = request.DisplayName ?? current.Name,
                VersionId = request.VersionId ?? current.VersionId,
                Loader = request.Loader ?? current.Loader,
                LoaderVersion = request.LoaderVersion ?? current.LoaderVersion,
                Isolation = request.Isolation ?? current.Isolation,
            };
        }

        progress.Report(new InstallProgress(InstallStage.Done, 100, 0, 0, 0, null, "已保存实例设置"));
    }

    public async Task RepairInstanceAsync(
        string instanceId, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        progress.Report(new InstallProgress(InstallStage.Finalizing, 50, 0, 0, 0, null, "模拟修复"));
        await Task.Delay(150, ct);
        progress.Report(new InstallProgress(InstallStage.Done, 100, 0, 0, 0, null, "模拟修复完成"));
    }

    public Task<InstanceSummary> InstallModpackAsync(
        ModpackInstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        lock (_instanceSync)
        {
            return Task.FromResult(_instances[0]);
        }
    }

    public Task<GameSession> LaunchAsync(
        string instanceId, IProgress<LaunchEvent> events, CancellationToken ct)
    {
        var exit = new TaskCompletionSource<int>();
        _ = Task.Run(async () =>
        {
            events.Report(new LaunchEvent(LaunchEventKind.Started, "游戏进程已启动（Fake）"));
            await Task.Delay(300, ct);
            events.Report(new LaunchEvent(LaunchEventKind.Stdout, "[Fake] 加载世界..."));
            await Task.Delay(300, ct);
            events.Report(new LaunchEvent(LaunchEventKind.Exited, "游戏已退出（Fake）", 0));
            exit.SetResult(0);
        }, ct);

        return Task.FromResult(new GameSession(instanceId, 4242, exit.Task));
    }

    public Task StopGameAsync(string instanceId, StopMode mode, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<RunningSession>> GetRunningSessionsAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<RunningSession>>([]);

    public async Task<AccountSummary> LoginAsync(
        LoginRequest request, IProgress<LoginEvent> progress, CancellationToken ct)
    {
        AccountSummary account;
        switch (request.Mode)
        {
            case AuthMode.Offline:
            {
                var name = string.IsNullOrWhiteSpace(request.Username) ? "Player" : request.Username.Trim();
                account = new AccountSummary(
                    $"offline:{name}", AuthMode.Offline, name,
                    UuidFor(name), null, true, null);
                progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"离线账户：{name}"));
                break;
            }

            case AuthMode.Microsoft:
            {
                progress.Report(new LoginEvent(
                    LoginEventKind.DeviceCode,
                    "请在浏览器打开 https://www.microsoft.com/link 并输入代码 FAKE-1234",
                    "FAKE-1234",
                    "https://www.microsoft.com/link"));
                await Task.Delay(300, ct);
                progress.Report(new LoginEvent(LoginEventKind.WaitingForUser, "等待 Microsoft 登录完成…"));
                await Task.Delay(300, ct);
                account = new AccountSummary(
                    "msa:fake", AuthMode.Microsoft, "Steve",
                    UuidFor("msa:fake"), null, true, DateTimeOffset.Now.AddDays(1));
                progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"Microsoft 账户：{account.Username}"));
                break;
            }

            case AuthMode.Authlib:
            {
                if (string.IsNullOrWhiteSpace(request.ServerUrl) ||
                    string.IsNullOrWhiteSpace(request.Username) ||
                    string.IsNullOrWhiteSpace(request.Password))
                {
                    throw new AuthException("E6005", "外置登录需要服务器地址、用户名与密码。");
                }

                var host = new Uri(request.ServerUrl).Host;
                var name = request.Username.Trim();
                account = new AccountSummary(
                    $"authlib:{name}@{host}", AuthMode.Authlib, name,
                    UuidFor($"{name}@{host}"), null, true, null);
                progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"外置账户：{name}"));
                break;
            }

            default:
                throw new ArgumentOutOfRangeException(nameof(request), request.Mode, "未知的登录方式。");
        }

        lock (_accountSync)
        {
            _accounts.RemoveAll(a => a.Id == account.Id);
            _accounts.Add(account);
            _activeAccountId = account.Id;
        }

        return account with { IsActive = true };
    }

    public Task LogoutAsync(string accountId, CancellationToken ct)
    {
        lock (_accountSync)
        {
            _accounts.RemoveAll(a => a.Id == accountId);
            if (_activeAccountId == accountId)
            {
                _activeAccountId = _accounts.FirstOrDefault()?.Id;
            }
        }

        return Task.CompletedTask;
    }

    public Task SetActiveAccountAsync(string accountId, CancellationToken ct)
    {
        lock (_accountSync)
        {
            if (!_accounts.Any(a => a.Id == accountId))
            {
                throw new KeyNotFoundException($"账户不存在：{accountId}");
            }

            _activeAccountId = accountId;
        }

        return Task.CompletedTask;
    }

    private static string UuidFor(string value)
    {
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexStringLower(hash);
    }

    private static readonly List<ModEntry> FakeMods =
    [
        new ModEntry("sodium", "Sodium", "0.6.0", "sodium.jar", true, LoaderKind.Fabric, [], null),
        new ModEntry("iris", "Iris", "1.8.0", "iris.jar", true, LoaderKind.Fabric, ["sodium"], null),
    ];

    public Task<IReadOnlyList<ModEntry>> ScanModsAsync(string instanceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ModEntry>>(FakeMods.ToArray());

    public Task SetModEnabledAsync(string instanceId, string modId, bool enabled, CancellationToken ct)
    {
        var index = FakeMods.FindIndex(m => m.Id == modId);
        if (index >= 0)
        {
            FakeMods[index] = FakeMods[index] with { Enabled = enabled };
        }

        return Task.CompletedTask;
    }

    public Task DeleteModAsync(string instanceId, string modId, CancellationToken ct)
    {
        FakeMods.RemoveAll(m => m.Id == modId);
        return Task.CompletedTask;
    }

    public Task ImportModAsync(string instanceId, string sourceJarPath, CancellationToken ct)
    {
        var name = Path.GetFileNameWithoutExtension(sourceJarPath);
        FakeMods.Add(new ModEntry(
            name, name, null, Path.GetFileName(sourceJarPath), true, LoaderKind.Fabric, [], null));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<OnlineModSummary>> SearchOnlineModsAsync(
        ModSource source, string query, string? modCategory, LoaderKind? loader, string? gameVersion, int page, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<OnlineModSummary>>(
        [
            new OnlineModSummary("sodium", "sodium", "Sodium", "现代渲染优化，大幅提升帧率", "jellysquid3", 42000000, null),
            new OnlineModSummary("iris", "iris", "Iris Shaders", "高性能光影加载器，兼容主流光影包", "coderbot", 18000000, null),
        ]);

    public Task<string> InstallOnlineModAsync(
        string instanceId, ModSource source, string projectId, string? gameVersion, LoaderKind loader,
        IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var fileName = projectId + ".jar";
        FakeMods.Add(new ModEntry(
            projectId, projectId, null, fileName, true, loader, [], null));
        return Task.FromResult(fileName);
    }

    public Task DeleteInstanceAsync(string instanceId, bool keepSaves, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<InstanceSummary>> ImportExternalAsync(
        string path, IProgress<ImportProgress> progress, CancellationToken ct)
    {
        lock (_instanceSync)
        {
            return Task.FromResult<IReadOnlyList<InstanceSummary>>(_instances.ToArray());
        }
    }

    public Task UpdateSettingsAsync(AppSettingsPatch patch, CancellationToken ct) => Task.CompletedTask;

    public Task<UpdateInfo?> CheckUpdateAsync(CancellationToken ct) =>
        Task.FromResult<UpdateInfo?>(null);

    public Task ApplyUpdateAsync(UpdateInfo info, IProgress<UpdateProgress> progress, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<IntegrityReport> VerifyIntegrityAsync(IntegrityCheckMode mode, CancellationToken ct) =>
        Task.FromResult(new IntegrityReport(
            true, mode, 2, Array.Empty<string>(), "假数据：校验通过。", DateTimeOffset.Now));

    public Task<IReadOnlyList<LogFileInfo>> GetLogFilesAsync(string? instanceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LogFileInfo>>(
            [new LogFileInfo(@"C:\Lumen\logs\launcher-20261024.log", null, LogKind.Launcher, 4096, DateTimeOffset.Now)]);

    public Task<string> ExportDiagnosticsAsync(string? instanceId, CancellationToken ct) =>
        Task.FromResult(@"C:\Lumen\exports\diagnostics-fake.zip");
}
