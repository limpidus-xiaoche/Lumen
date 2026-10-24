namespace Launcher.Protocol;

/// <summary>
/// UI 唯一依赖的门面。Core 内部接口可自由重构，但不得破坏本接口与 DTO。
/// </summary>
public interface ILauncherFacade
{
    // 快照 / 元数据
    Task<LauncherSnapshot> GetSnapshotAsync(CancellationToken ct);
    Task<VersionManifest> RefreshManifestAsync(ManifestRefreshMode mode, CancellationToken ct);
    Task<IReadOnlyList<LoaderVersionInfo>> GetLoaderVersionsAsync(
        LoaderKind loader, string gameVersion, CancellationToken ct);

    // 实例 / 任务
    Task<InstanceDetail> GetInstanceAsync(string instanceId, CancellationToken ct);
    Task<IReadOnlyList<TaskInfo>> GetTasksAsync(CancellationToken ct);
    Task CancelTaskAsync(string taskId, CancellationToken ct);
    Task RetryTaskAsync(string taskId, CancellationToken ct);

    // 安装 / 修复 / 加载器变更
    Task<InstanceSummary> InstallAsync(
        InstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct);
    Task UpdateInstanceAsync(
        string instanceId, UpdateInstanceRequest request, IProgress<InstallProgress> progress, CancellationToken ct);
    Task RepairInstanceAsync(
        string instanceId, IProgress<InstallProgress> progress, CancellationToken ct);
    Task<InstanceSummary> InstallModpackAsync(
        ModpackInstallRequest request, IProgress<InstallProgress> progress, CancellationToken ct);

    // 启动 / 结束
    Task<GameSession> LaunchAsync(
        string instanceId, IProgress<LaunchEvent> events, CancellationToken ct);
    Task StopGameAsync(string instanceId, StopMode mode, CancellationToken ct);
    Task<IReadOnlyList<RunningSession>> GetRunningSessionsAsync(CancellationToken ct);

    // 账户
    Task<AccountSummary> LoginAsync(
        LoginRequest request, IProgress<LoginEvent> progress, CancellationToken ct);
    Task LogoutAsync(string accountId, CancellationToken ct);
    Task SetActiveAccountAsync(string accountId, CancellationToken ct);

    // 模组
    Task<IReadOnlyList<ModEntry>> ScanModsAsync(string instanceId, CancellationToken ct);
    Task SetModEnabledAsync(string instanceId, string modId, bool enabled, CancellationToken ct);

    // 实例运维
    Task DeleteInstanceAsync(string instanceId, bool keepSaves, CancellationToken ct);
    Task<IReadOnlyList<InstanceSummary>> ImportExternalAsync(
        string path, IProgress<ImportProgress> progress, CancellationToken ct);

    // 设置
    Task UpdateSettingsAsync(AppSettingsPatch patch, CancellationToken ct);

    // 更新
    Task<UpdateInfo?> CheckUpdateAsync(CancellationToken ct);
    Task ApplyUpdateAsync(UpdateInfo info, IProgress<UpdateProgress> progress, CancellationToken ct);

    // 日志 / 诊断
    Task<IReadOnlyList<LogFileInfo>> GetLogFilesAsync(string? instanceId, CancellationToken ct);
    Task<string> ExportDiagnosticsAsync(string? instanceId, CancellationToken ct);
}

public enum ManifestRefreshMode
{
    IfStale,
    Force,
}
