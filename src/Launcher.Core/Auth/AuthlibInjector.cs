using Launcher.Core.Download;
using Launcher.Core.Storage;

namespace Launcher.Core.Auth;

public sealed class AuthlibInjector
{
    public const string Version = "1.2.8";
    public const string Sha1 = "0e0e66d8a4f91a26f33b9c09f5cdffce4a11f0b8";
    public const string DownloadUrl =
        "https://github.com/yushijinhun/authlib-injector/releases/download/v1.2.8/authlib-injector-1.2.8.jar";

    private readonly LauncherPaths _paths;
    private readonly DownloadEngine _downloads;

    public AuthlibInjector(LauncherPaths paths, DownloadEngine downloads)
    {
        _paths = paths;
        _downloads = downloads;
    }

    public async Task<string> EnsureAsync(CancellationToken ct)
    {
        var dir = Path.Combine(_paths.SharedDir, "authlib-injector");
        var path = Path.Combine(dir, $"authlib-injector-{Version}.jar");
        await _downloads.DownloadAsync(
            new DownloadItem(DownloadUrl, path, Sha1, 0, DownloadKind.Loader), null, ct);
        return path;
    }

    public async Task<string> BuildJavaAgentAsync(string serverUrl, CancellationToken ct)
    {
        var path = await EnsureAsync(ct);
        return $"-javaagent:{path}={serverUrl.TrimEnd('/')}";
    }
}
