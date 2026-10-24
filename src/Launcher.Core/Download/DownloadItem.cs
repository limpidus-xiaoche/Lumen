namespace Launcher.Core.Download;

public enum DownloadKind
{
    VersionJson,
    ClientJar,
    Library,
    Native,
    Asset,
    Loader,
    Other,
}

public sealed record DownloadItem(
    string Url,
    string DestPath,
    string? Sha1,
    long Size,
    DownloadKind Kind = DownloadKind.Other,
    IReadOnlyList<string>? Mirrors = null);

public sealed record DownloadProgress(
    long BytesDownloaded,
    long TotalBytes,
    double SpeedBytesPerSec,
    string CurrentFile);
