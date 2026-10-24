using Launcher.Protocol;

namespace Launcher.App.Mock;

/// <summary>
/// 内核未就绪时的假实现：保证 UI 可以独立推进（M0）。
/// </summary>
public sealed class FakeLauncherFacade : ILauncherFacade
{
    private readonly AppSettings _settings = new() { GameDir = @"C:\Lumen\game" };

    private static readonly InstanceSummary[] FakeInstances =
    [
        new("demo-vanilla", "原版 1.21.1", "1.21.1", LoaderKind.Vanilla, null,
            IsolationLevel.ModsConfig, InstanceState.Ready, null, DateTimeOffset.Now.AddDays(-1)),
        new("demo-fabric", "Fabric 1.20.1", "1.20.1", LoaderKind.Fabric, "0.16.0",
            IsolationLevel.ModsConfig, InstanceState.Ready, null, DateTimeOffset.Now.AddDays(-3)),
    ];

    public Task<LauncherSnapshot> GetSnapshotAsync(CancellationToken ct) =>
        Task.FromResult(new LauncherSnapshot(
            _settings,
            FakeInstances,
            [new AccountSummary("offline-demo", AuthMode.Offline, "Player", "00000000000030008000000000000000", null, true, null)],
            "offline-demo",
            [new JavaRuntime(@"C:\Java\jdk-21\bin\java.exe", 21, true, "Temurin", "21.0.8")],
            DateTimeOffset.Now.AddMinutes(-5),
            "0.1.0-m0"));

    public Task<VersionManifest> RefreshManifestAsync(ManifestRefreshMode mode, CancellationToken ct) =>
        Task.FromResult(new VersionManifest("1.21.1",
        [
            new VersionSummary("1.21.1", "release", "https://example.invalid/1.21.1.json", null),
            new VersionSummary("1.20.1", "release", "https://example.invalid/1.20.1.json", null),
        ]));

    public Task<IReadOnlyList<LoaderVersionInfo>> GetLoaderVersionsAsync(
        LoaderKind loader, string gameVersion, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LoaderVersionInfo>>(
            [new LoaderVersionInfo("0.16.0", true, DateTimeOffset.Now.AddDays(-30))]);

    public Task<InstanceDetail> GetInstanceAsync(string instanceId, CancellationToken ct)
    {
        var summary = FakeInstances.First(i => i.Id == instanceId);
        return Task.FromResult(new InstanceDetail(
            summary, Path.Combine(@"C:\Lumen\instances", instanceId), null, 4096, string.Empty, 12, 1_234_567));
    }

    public Task<IReadOnlyList<TaskInfo>> GetTasksAsync(CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<TaskInfo>>([]);

    public Task CancelTaskAsync(string taskId, CancellationToken ct) => Task.CompletedTask;

    public Task RetryTaskAsync(string taskId, CancellationToken ct) => Task.CompletedTask;

    public async Task<InstanceSummary> InstallAsync(
        InstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        var stages = new[]
        {
            InstallStage.VersionJson, InstallStage.ClientJar, InstallStage.Libraries,
            InstallStage.Natives, InstallStage.Assets, InstallStage.Loader, InstallStage.Finalizing,
        };

        // 契约约定：Fake 每 200ms 报一次假进度；前期阶段总量未知，Percent = -1（不确定态）
        for (var i = 0; i < stages.Length; i++)
        {
            var percent = i < 2 ? -1 : (i + 1) * 100d / stages.Length;
            progress.Report(new InstallProgress(
                stages[i], percent, 0, 0, 0, null, $"模拟安装：{stages[i]}"));
            await Task.Delay(200, ct);
        }

        progress.Report(new InstallProgress(InstallStage.Done, 100, 0, 0, 0, null, "模拟安装完成"));
        return FakeInstances[0];
    }

    public Task UpdateInstanceAsync(
        string instanceId, UpdateInstanceRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        Task.CompletedTask;

    public async Task RepairInstanceAsync(
        string instanceId, IProgress<InstallProgress> progress, CancellationToken ct)
    {
        progress.Report(new InstallProgress(InstallStage.Finalizing, 50, 0, 0, 0, null, "模拟修复"));
        await Task.Delay(150, ct);
        progress.Report(new InstallProgress(InstallStage.Done, 100, 0, 0, 0, null, "模拟修复完成"));
    }

    public Task<InstanceSummary> InstallModpackAsync(
        ModpackInstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct) =>
        Task.FromResult(FakeInstances[1]);

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

    public Task<AccountSummary> LoginAsync(
        LoginRequest request, IProgress<LoginEvent> progress, CancellationToken ct)
    {
        if (request.Mode != AuthMode.Offline)
        {
            throw new NotSupportedException("Fake 仅支持离线账户。");
        }

        var name = request.Username ?? "Player";
        progress.Report(new LoginEvent(LoginEventKind.Succeeded, $"离线登录：{name}"));
        return Task.FromResult(new AccountSummary(
            "offline-" + name, AuthMode.Offline, name, "00000000000030008000000000000000",
            null, true, null));
    }

    public Task LogoutAsync(string accountId, CancellationToken ct) => Task.CompletedTask;

    public Task SetActiveAccountAsync(string accountId, CancellationToken ct) => Task.CompletedTask;

    public Task<IReadOnlyList<ModEntry>> ScanModsAsync(string instanceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<ModEntry>>(
        [
            new ModEntry("sodium", "Sodium", "0.6.0", "sodium.jar", true, LoaderKind.Fabric, [], null),
            new ModEntry("iris", "Iris", "1.8.0", "iris.jar", true, LoaderKind.Fabric, ["sodium"], null),
        ]);

    public Task SetModEnabledAsync(string instanceId, string modId, bool enabled, CancellationToken ct) =>
        Task.CompletedTask;

    public Task DeleteInstanceAsync(string instanceId, bool keepSaves, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<InstanceSummary>> ImportExternalAsync(
        string path, IProgress<ImportProgress> progress, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<InstanceSummary>>(FakeInstances);

    public Task UpdateSettingsAsync(AppSettingsPatch patch, CancellationToken ct) => Task.CompletedTask;

    public Task<UpdateInfo?> CheckUpdateAsync(CancellationToken ct) =>
        Task.FromResult<UpdateInfo?>(null);

    public Task ApplyUpdateAsync(UpdateInfo info, IProgress<UpdateProgress> progress, CancellationToken ct) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<LogFileInfo>> GetLogFilesAsync(string? instanceId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<LogFileInfo>>(
            [new LogFileInfo(@"C:\Lumen\logs\launcher-20261024.log", null, LogKind.Launcher, 4096, DateTimeOffset.Now)]);

    public Task<string> ExportDiagnosticsAsync(string? instanceId, CancellationToken ct) =>
        Task.FromResult(@"C:\Lumen\exports\diagnostics-fake.zip");
}
