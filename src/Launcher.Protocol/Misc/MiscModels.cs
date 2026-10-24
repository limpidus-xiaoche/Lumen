namespace Launcher.Protocol;

public sealed record JavaRuntime(
    string Path,
    int MajorVersion,
    bool Is64Bit,
    string Vendor,
    string? VersionString);

public sealed record ModEntry(
    string Id,
    string Name,
    string? Version,
    string FileName,
    bool Enabled,
    LoaderKind Loader,
    IReadOnlyList<string> Dependencies,
    string? IconPath);

/// <summary>在线模组下载渠道。</summary>
public enum ModSource
{
    Modrinth,
    CurseForge,
}

/// <summary>在线模组搜索结果（Modrinth / CurseForge）。</summary>
public sealed record OnlineModSummary(
    string ProjectId,
    string Slug,
    string Title,
    string Description,
    string? Author,
    long Downloads,
    string? IconPath);

public enum ModpackKind
{
    Modrinth,
    Hmcl,
    MultiMc,
    CurseForgeDirect,
}

public sealed record ModpackInstallRequest(
    string FilePath,
    string? InstanceName,
    IsolationLevel Isolation,
    ModpackKind Kind);

public sealed record UpdateInfo(
    string Version,
    string Notes,
    long Size,
    string Sha256,
    string Url);

public sealed record UpdateProgress(double Percent, string Message);

public enum LogKind
{
    Launcher,
    Game,
}

public sealed record LogFileInfo(
    string Path,
    string? InstanceId,
    LogKind Kind,
    long Size,
    DateTimeOffset LastWriteAt);
