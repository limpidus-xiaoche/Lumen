using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using Launcher.Core.Download;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Launcher.Core.Update;

/// <summary>
/// 自更新（§12.1 + M8）。更新清单走 GitHub Releases 的 HTTPS 直链，
/// 下载后先比 size、再流式比 sha256，任何失败 fail-closed（E8001）。
/// </summary>
public sealed class UpdateService
{
    public const string DefaultChannel = "win";
    private const string DefaultManifestBase =
        "https://github.com/limpidus-xiaoche/Lumen/releases/latest/download";

    private readonly LauncherPaths _paths;
    private readonly ILogger<UpdateService> _logger;
    private readonly HttpClient _http;
    private readonly IntegrityService? _integrity;
    private readonly string _currentVersion;
    private readonly string _manifestBase;

    public UpdateService(
        LauncherPaths paths,
        ILogger<UpdateService>? logger = null,
        HttpClient? httpClient = null,
        IntegrityService? integrity = null,
        string? currentVersion = null,
        string? manifestBaseUrl = null)
    {
        _paths = paths;
        _logger = logger ?? NullLogger<UpdateService>.Instance;
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
        _integrity = integrity;
        _currentVersion = currentVersion
                         ?? typeof(UpdateService).Assembly.GetName().Version?.ToString()
                         ?? "0.0.0";
        _manifestBase = (manifestBaseUrl ?? DefaultManifestBase).TrimEnd('/');
    }

    public string CurrentVersion => _currentVersion;

    public string BuildManifestUrl(string? channel)
    {
        var name = string.IsNullOrWhiteSpace(channel) ? DefaultChannel : channel.Trim();
        if (!IsSafeSegment(name))
        {
            throw new UpdateException($"非法的更新通道：{channel}");
        }

        return $"{_manifestBase}/releases.{name}.json";
    }

    /// <summary>检查更新；默认拒绝降级，<paramref name="force"/> 为真时允许。</summary>
    public async Task<UpdateInfo?> CheckAsync(
        string? channel = null, bool force = false, CancellationToken ct = default)
    {
        var manifestUrl = BuildManifestUrl(channel);
        UrlGuard.Validate(manifestUrl);

        string json;
        try
        {
            using var response = await _http.GetAsync(manifestUrl, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateException($"拉取更新清单失败：HTTP {(int)response.StatusCode}");
            }

            json = await response.Content.ReadAsStringAsync(ct);
        }
        catch (UpdateException)
        {
            throw;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException("拉取更新清单失败（网络错误）。", ex);
        }

        var manifest = ReleaseManifestParser.Parse(json);
        var selected = SelectUpdate(manifest, _currentVersion, force);
        if (selected is null)
        {
            _logger.LogInformation(
                "无可用更新：清单 {Remote}，当前 {Current}", manifest.Version, _currentVersion);
            return null;
        }

        if (manifest.MinSupported is not null &&
            AppVersion.Compare(_currentVersion, manifest.MinSupported) < 0)
        {
            _logger.LogWarning(
                "当前版本 {Current} 低于清单 minSupported {Min}，建议手动安装",
                _currentVersion, manifest.MinSupported);
        }

        return selected;
    }

    /// <summary>版本单调：等于当前版本或降级（且未显式 force）时返回 null。</summary>
    public static UpdateInfo? SelectUpdate(
        ReleaseManifest manifest, string currentVersion, bool force = false)
    {
        var comparison = AppVersion.Compare(manifest.Version, currentVersion);
        if (comparison == 0 || (comparison < 0 && !force))
        {
            return null;
        }

        return new UpdateInfo(
            manifest.Version,
            manifest.Notes ?? string.Empty,
            manifest.Size,
            manifest.Sha256,
            manifest.Url);
    }

    /// <summary>下载更新包并完成 size + sha256 校验，返回本地路径。失败重试 2 次。</summary>
    public async Task<string> DownloadPackageAsync(
        UpdateInfo info, IProgress<UpdateProgress>? progress = null, CancellationToken ct = default)
    {
        UrlGuard.Validate(info.Url);
        if (info.Size <= 0 || !IntegrityManifest.IsSha256Hex(info.Sha256))
        {
            throw new UpdateException("更新包元数据缺少 size 或 sha256。");
        }

        var version = IsSafeSegment(info.Version) ? info.Version : "unknown";
        var directory = Path.Combine(_paths.CacheDir, "updates", version);
        Directory.CreateDirectory(directory);

        var fileName = Path.GetFileName(Uri.UnescapeDataString(new Uri(info.Url).LocalPath));
        if (string.IsNullOrWhiteSpace(fileName) || !IsSafeSegment(fileName))
        {
            fileName = $"Lumen-{version}.exe";
        }

        var target = Path.Combine(directory, fileName);
        const int maxAttempts = 3;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await DownloadOnceAsync(info, target, progress, attempt, ct);
                _logger.LogInformation("更新包校验通过：{Path}", target);
                return target;
            }
            catch (UpdateException) when (attempt < maxAttempts)
            {
                _logger.LogWarning("更新包校验失败，第 {Attempt} 次重试", attempt);
                await Task.Delay(TimeSpan.FromSeconds(attempt), ct);
            }
        }

        throw new UpdateException("更新包多次校验失败，请手动下载。");
    }

    private async Task DownloadOnceAsync(
        UpdateInfo info, string target, IProgress<UpdateProgress>? progress, int attempt,
        CancellationToken ct)
    {
        var temp = target + ".part";
        try
        {
            using var response = await _http.GetAsync(
                info.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
            {
                throw new UpdateException($"下载更新包失败：HTTP {(int)response.StatusCode}");
            }

            progress?.Report(new UpdateProgress(0, $"开始下载更新包（第 {attempt} 次）"));

            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var destination = File.Create(temp);

            var buffer = new byte[81920];
            long received = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, ct)) > 0)
            {
                await destination.WriteAsync(buffer.AsMemory(0, read), ct);
                sha.AppendData(buffer, 0, read);
                received += read;
                var percent = info.Size > 0
                    ? Math.Clamp(received * 100.0 / info.Size, 0, 100)
                    : -1;
                progress?.Report(new UpdateProgress(percent, $"已下载 {received} / {info.Size} 字节"));
            }

            await destination.FlushAsync(ct);
            var hash = Convert.ToHexStringLower(sha.GetHashAndReset());
            var length = new FileInfo(temp).Length;
            if (length != info.Size)
            {
                throw new UpdateException($"更新包 size 不符：实际 {length}，期望 {info.Size}");
            }

            if (!string.Equals(hash, info.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                throw new UpdateException("更新包 sha256 不符。");
            }

            File.Move(temp, target, overwrite: true);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new UpdateException("下载更新包失败（网络错误）。", ex);
        }
        finally
        {
            if (File.Exists(temp))
            {
                File.Delete(temp);
            }
        }
    }

    /// <summary>
    /// 应用更新：先校验自身完整性（E8002），再校验更新包（E8001），
    /// 通过后才启动 Setup.exe；便携 zip 不自动替换，返回路径由用户手动处理。
    /// </summary>
    public async Task<string> ApplyAsync(
        UpdateInfo info, IProgress<UpdateProgress>? progress = null, CancellationToken ct = default)
    {
        if (_integrity is not null)
        {
            await _integrity.VerifyAsync(IntegrityCheckMode.Quick, force: false, ct);
        }

        var package = await DownloadPackageAsync(info, progress, ct);
        if (!package.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(new UpdateProgress(100, "便携更新包已下载，请手动替换。"));
            return package;
        }

        progress?.Report(new UpdateProgress(100, "启动安装程序。"));
        Process.Start(new ProcessStartInfo
        {
            FileName = package,
            Arguments = "--silent",
            UseShellExecute = true,
        });
        return package;
    }

    private static bool IsSafeSegment(string value) =>
        value.Length is > 0 and <= 64 &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_');
}
