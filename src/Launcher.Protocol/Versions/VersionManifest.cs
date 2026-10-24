namespace Launcher.Protocol;

public sealed record VersionManifest(string LatestRelease, IReadOnlyList<VersionSummary> Versions);

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
