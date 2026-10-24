namespace Launcher.Protocol;

public sealed record VersionManifest(LatestVersions? Latest, IReadOnlyList<VersionSummary> Versions)
{
    public string LatestRelease => Latest?.Release ?? string.Empty;
}

public sealed record LatestVersions(string Release, string? Snapshot);

public sealed record VersionSummary(string Id, string Type, string Url, string? Sha1);

public enum LoaderKind
{
    Vanilla,
    Fabric,
    Forge,
    NeoForge,
    Quilt,
}

public sealed record LoaderVersionInfo(string Version, bool Stable, DateTimeOffset? ReleasedAt);
