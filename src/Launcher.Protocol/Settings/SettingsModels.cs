namespace Launcher.Protocol;

public enum ProxyMode
{
    System,
    Manual,
    None,
}

public enum DownloadSourceMode
{
    Auto,
    Official,
    Mirror,
}

public sealed record AppSettings
{
    public string GameDir { get; init; } = string.Empty;
    public string? JavaPath { get; init; }
    public int MaxMemoryMb { get; init; } = 4096;
    public int MinMemoryMb { get; init; } = 1024;
    public ProxyMode ProxyMode { get; init; } = ProxyMode.System;
    public string? ProxyAddress { get; init; }
    public DownloadSourceMode DownloadSource { get; init; } = DownloadSourceMode.Auto;
    public int MaxThreads { get; init; } = 16;
    public bool AutoUpdate { get; init; } = true;
    public string AccentColor { get; init; } = "#4A9EFF";
}

public sealed record AppSettingsPatch(
    int? MaxMemoryMb = null,
    string? GameDir = null,
    string? JavaPath = null,
    ProxyMode? ProxyMode = null,
    string? ProxyAddress = null,
    DownloadSourceMode? DownloadSource = null,
    int? MaxThreads = null,
    bool? AutoUpdate = null,
    string? AccentColor = null);

public sealed record LauncherSnapshot(
    AppSettings Settings,
    IReadOnlyList<InstanceSummary> Instances,
    IReadOnlyList<AccountSummary> Accounts,
    string? ActiveAccountId,
    IReadOnlyList<JavaRuntime> Javas,
    DateTimeOffset? ManifestUpdatedAt,
    string AppVersion);
