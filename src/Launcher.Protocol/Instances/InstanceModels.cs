namespace Launcher.Protocol;

public enum IsolationLevel
{
    None,
    ModsConfig,
    Full,
}

public enum InstanceState
{
    Installing,
    Ready,
    Broken,
    Repairing,
}

public enum InstallMode
{
    New,
    Reinstall,
    ChangeLoader,
}

public sealed record InstanceSummary(
    string Id,
    string Name,
    string VersionId,
    LoaderKind Loader,
    string? LoaderVersion,
    IsolationLevel Isolation,
    InstanceState State,
    string? IconPath,
    DateTimeOffset? LastPlayedAt);

public sealed record InstanceDetail(
    InstanceSummary Summary,
    string GameDir,
    string? JavaPath,
    int MemoryMb,
    string ExtraJvmArgs,
    int ModCount,
    long DiskUsageBytes);

public sealed record InstallRequest(
    string? InstanceId,
    string DisplayName,
    string VersionId,
    LoaderKind Loader,
    string? LoaderVersion,
    IsolationLevel Isolation,
    InstallMode Mode,
    bool SkipAssets = false);

public sealed record UpdateInstanceRequest(
    string? DisplayName,
    string? VersionId,
    LoaderKind? Loader,
    string? LoaderVersion,
    IsolationLevel? Isolation,
    int? MemoryMb);
