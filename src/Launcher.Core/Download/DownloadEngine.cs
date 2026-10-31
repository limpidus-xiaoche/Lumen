using System.Buffers;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using Launcher.Protocol;

namespace Launcher.Core.Download;

public sealed class DownloadEngine
{
    private const long ChunkThreshold = 16L * 1024 * 1024;
    private const int ChunkSize = 8 * 1024 * 1024;
    private const int MaxChunkConcurrency = 8;

    private static readonly JsonSerializerOptions MetaJson = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false,
    };

    private readonly HttpClient _http;
    private readonly Func<DownloadSourceMode> _sourceMode;

    public DownloadEngine(HttpClient? http = null, Func<DownloadSourceMode>? sourceMode = null)
    {
        _http = http ?? CreateHttpClient();
        _sourceMode = sourceMode ?? (static () => DownloadSourceMode.Auto);
    }

    public async Task DownloadAsync(
        DownloadItem item,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(item.DestPath)!);

        if (item.Sha1 is not null && File.Exists(item.DestPath))
        {
            var existingHash = await ComputeSha1Async(item.DestPath, ct);
            if (string.Equals(existingHash, item.Sha1, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            File.Delete(item.DestPath);
        }

        var candidates = BuildCandidates(item);
        DownloadException? last = null;

        foreach (var url in candidates)
        {
            UrlGuard.Validate(url);
            for (var attempt = 0; attempt < 2; attempt++)
            {
                ct.ThrowIfCancellationRequested();
                var temp = item.DestPath + ".part";
                var meta = temp + ".meta";

                try
                {
                    if (item.Size > ChunkThreshold)
                    {
                        await DownloadChunkedAsync(url, temp, item.Size, progress, ct);
                    }
                    else
                    {
                        await DownloadSingleAsync(url, temp, item.Size, progress, ct);
                    }

                    if (item.Sha1 is not null)
                    {
                        var hash = await ComputeSha1Async(temp, ct);
                        if (!string.Equals(hash, item.Sha1, StringComparison.OrdinalIgnoreCase))
                        {
                            File.Delete(temp);
                            File.Delete(meta);
                            throw new DownloadException("E2003", $"SHA1 校验失败：{item.DestPath}");
                        }
                    }

                    File.Move(temp, item.DestPath, overwrite: true);
                    File.Delete(meta);
                    return;
                }
                catch (DownloadException ex) when (ex.ErrorCode == "E2003")
                {
                    last = ex;
                    break;
                }
                catch (DownloadException ex)
                {
                    last = ex;
                    if (attempt == 0)
                    {
                        await Task.Delay(500, ct);
                    }
                }
                catch (HttpRequestException ex)
                {
                    last = new DownloadException("E2001", $"下载失败 {url}: {ex.Message}", ex);
                    if (attempt == 0)
                    {
                        await Task.Delay(500, ct);
                    }
                }
            }
        }

        throw last ?? new DownloadException("E2001", $"下载失败：{item.DestPath}");
    }

    private async Task DownloadSingleAsync(
        string url,
        string temp,
        long expectedSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var metaPath = temp + ".meta";
        var meta = ReadSingleMeta(metaPath);
        long existing = File.Exists(temp) ? new FileInfo(temp).Length : 0;
        if (meta is null || !string.Equals(meta.Url, url, StringComparison.OrdinalIgnoreCase))
        {
            SafeDelete(temp);
            existing = 0;
        }

        if (expectedSize > 0 && existing == expectedSize)
        {
            return;
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existing > 0)
        {
            request.Headers.Range = new RangeHeaderValue(existing, null);
        }

        using var response = await SendWithRedirectsAsync(request, ct);
        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable && existing == expectedSize)
        {
            return;
        }

        response.EnsureSuccessStatusCode();

        var append = existing > 0 && response.StatusCode == HttpStatusCode.PartialContent;
        if (!append)
        {
            existing = 0;
        }

        var total = expectedSize > 0
            ? expectedSize
            : (response.Content.Headers.ContentLength ?? 0) + existing;

        WriteSingleMeta(metaPath, new SingleMeta(
            url,
            total,
            response.Headers.ETag?.Tag,
            response.Content.Headers.LastModified?.ToString("O")));

        await using var file = new FileStream(
            temp,
            append ? FileMode.Append : FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            81920,
            FileOptions.Asynchronous);

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        await CopyWithProgressAsync(stream, file, existing, total, progress, temp, ct);
    }

    private async Task DownloadChunkedAsync(
        string url,
        string temp,
        long totalSize,
        IProgress<DownloadProgress>? progress,
        CancellationToken ct)
    {
        var metaPath = temp + ".meta";
        var meta = ReadChunkMeta(metaPath);
        if (meta is null ||
            !string.Equals(meta.Url, url, StringComparison.OrdinalIgnoreCase) ||
            meta.TotalSize != totalSize ||
            meta.ChunkSize != ChunkSize)
        {
            CleanupChunks(temp);
            meta = new ChunkMeta(url, totalSize, ChunkSize, null);
            WriteChunkMeta(metaPath, meta);
        }

        var chunkCount = (int)((totalSize + ChunkSize - 1) / ChunkSize);
        long downloaded = 0;
        for (var i = 0; i < chunkCount; i++)
        {
            var chunkPath = ChunkPath(temp, i);
            if (File.Exists(chunkPath))
            {
                downloaded += new FileInfo(chunkPath).Length;
            }
        }

        using var semaphore = new SemaphoreSlim(MaxChunkConcurrency);
        var tasks = new List<Task>();

        for (var i = 0; i < chunkCount; i++)
        {
            var index = i;
            var start = (long)index * ChunkSize;
            var end = Math.Min(totalSize - 1, start + ChunkSize - 1);
            var expected = end - start + 1;
            var chunkPath = ChunkPath(temp, index);

            if (File.Exists(chunkPath) && new FileInfo(chunkPath).Length == expected)
            {
                continue;
            }

            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(ct);
                try
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, url);
                    request.Headers.Range = new RangeHeaderValue(start, end);
                    using var response = await SendWithRedirectsAsync(request, ct);
                    if (response.StatusCode != HttpStatusCode.PartialContent)
                    {
                        throw new DownloadException("E2001", "服务器不支持 Range 分片下载");
                    }

                    await using var file = new FileStream(
                        chunkPath, FileMode.Create, FileAccess.Write, FileShare.None,
                        81920, FileOptions.Asynchronous);
                    await using var stream = await response.Content.ReadAsStreamAsync(ct);
                    await stream.CopyToAsync(file, 81920, ct);

                    if (new FileInfo(chunkPath).Length != expected)
                    {
                        throw new DownloadException("E2001", "分片长度不符");
                    }

                    var done = Interlocked.Add(ref downloaded, expected);
                    progress?.Report(new DownloadProgress(done, totalSize, 0, Path.GetFileName(temp)));
                }
                finally
                {
                    semaphore.Release();
                }
            }, ct));
        }

        await Task.WhenAll(tasks);

        await using var output = new FileStream(
            temp, FileMode.Create, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous);
        for (var i = 0; i < chunkCount; i++)
        {
            var chunkPath = ChunkPath(temp, i);
            await using (var input = File.OpenRead(chunkPath))
            {
                await input.CopyToAsync(output, ct);
            }

            File.Delete(chunkPath);
        }
    }

    private async Task<HttpResponseMessage> SendWithRedirectsAsync(
        HttpRequestMessage request,
        CancellationToken ct)
    {
        var current = request;
        for (var hop = 0; hop < 4; hop++)
        {
            var response = await _http.SendAsync(current, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!IsRedirect(response.StatusCode))
            {
                return response;
            }

            var location = response.Headers.Location
                ?? throw new DownloadException("E2001", "重定向缺少 Location");
            var nextUri = location.IsAbsoluteUri
                ? location
                : new Uri(current.RequestUri!, location);
            UrlGuard.Validate(nextUri.ToString());
            response.Dispose();

            var next = new HttpRequestMessage(current.Method, nextUri);
            foreach (var header in current.Headers)
            {
                next.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            current = next;
        }

        throw new DownloadException("E2001", "重定向次数过多");
    }

    private static bool IsRedirect(HttpStatusCode status) =>
        status is HttpStatusCode.MovedPermanently
            or HttpStatusCode.Redirect
            or HttpStatusCode.RedirectMethod
            or HttpStatusCode.TemporaryRedirect
            or HttpStatusCode.PermanentRedirect;

    private static async Task CopyWithProgressAsync(
        Stream input,
        Stream output,
        long initial,
        long total,
        IProgress<DownloadProgress>? progress,
        string tempPath,
        CancellationToken ct)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        var stopwatch = Stopwatch.StartNew();
        var lastReport = TimeSpan.Zero;
        long done = initial;

        try
        {
            int read;
            while ((read = await input.ReadAsync(buffer, ct)) > 0)
            {
                await output.WriteAsync(buffer.AsMemory(0, read), ct);
                done += read;

                if (stopwatch.Elapsed - lastReport > TimeSpan.FromMilliseconds(200))
                {
                    lastReport = stopwatch.Elapsed;
                    progress?.Report(new DownloadProgress(
                        done,
                        total,
                        stopwatch.Elapsed.TotalSeconds > 0 ? done / stopwatch.Elapsed.TotalSeconds : 0,
                        Path.GetFileName(tempPath)));
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private static async Task<string> ComputeSha1Async(string path, CancellationToken ct)
    {
        using var sha = SHA1.Create();
        await using var file = File.OpenRead(path);
        var buffer = ArrayPool<byte>.Shared.Rent(81920);
        try
        {
            int read;
            while ((read = await file.ReadAsync(buffer, ct)) > 0)
            {
                sha.TransformBlock(buffer, 0, read, null, 0);
            }

            sha.TransformFinalBlock([], 0, 0);
            return Convert.ToHexString(sha.Hash!).ToLowerInvariant();
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    private IReadOnlyList<string> BuildCandidates(DownloadItem item) => BuildCandidates(item, _sourceMode());

    /// <summary>
    /// 按下载源策略生成候选地址：Auto 保持原有顺序，Official 不追加 BMCLAPI，
    /// Mirror 优先 BMCLAPI 与显式镜像、原始地址作最后兜底。
    /// </summary>
    public static IReadOnlyList<string> BuildCandidates(DownloadItem item, DownloadSourceMode mode)
    {
        var result = new List<string>();
        var mirrored = MirrorUrl(item.Url);
        var hasMirrored = !string.Equals(mirrored, item.Url, StringComparison.OrdinalIgnoreCase);

        switch (mode)
        {
            case DownloadSourceMode.Official:
                result.Add(item.Url);
                if (item.Mirrors is not null)
                {
                    result.AddRange(item.Mirrors);
                }

                break;
            case DownloadSourceMode.Mirror:
                if (hasMirrored)
                {
                    result.Add(mirrored);
                }

                if (item.Mirrors is not null)
                {
                    result.AddRange(item.Mirrors);
                }

                result.Add(item.Url);
                break;
            default:
                result.Add(item.Url);
                if (item.Mirrors is not null)
                {
                    result.AddRange(item.Mirrors);
                }

                if (hasMirrored)
                {
                    result.Add(mirrored);
                }

                break;
        }

        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    public static string MirrorUrl(string url) =>
        url.Replace("https://piston-meta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
           .Replace("https://launchermeta.mojang.com", "https://bmclapi2.bangbang93.com", StringComparison.OrdinalIgnoreCase)
           .Replace("https://libraries.minecraft.net", "https://bmclapi2.bangbang93.com/maven", StringComparison.OrdinalIgnoreCase)
           .Replace("https://resources.download.minecraft.net", "https://bmclapi2.bangbang93.com/assets", StringComparison.OrdinalIgnoreCase);

    private static string ChunkPath(string temp, int index) => $"{temp}.chunk{index}";

    private static void CleanupChunks(string temp)
    {
        foreach (var file in Directory.GetFiles(Path.GetDirectoryName(temp)!, Path.GetFileName(temp) + ".chunk*"))
        {
            File.Delete(file);
        }
    }

    private static void SafeDelete(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private static SingleMeta? ReadSingleMeta(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<SingleMeta>(File.ReadAllText(path), MetaJson)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteSingleMeta(string path, SingleMeta meta) =>
        File.WriteAllText(path, JsonSerializer.Serialize(meta, MetaJson));

    private static ChunkMeta? ReadChunkMeta(string path)
    {
        try
        {
            return File.Exists(path)
                ? JsonSerializer.Deserialize<ChunkMeta>(File.ReadAllText(path), MetaJson)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteChunkMeta(string path, ChunkMeta meta) =>
        File.WriteAllText(path, JsonSerializer.Serialize(meta, MetaJson));

    private static HttpClient CreateHttpClient()
    {
        var handler = new HttpClientHandler { AllowAutoRedirect = false };
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(60) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Lumen/0.1 (+https://github.com/limpidus-xiaoche/Lumen)");
        return client;
    }

    private sealed record SingleMeta(string Url, long TotalSize, string? ETag, string? LastModified);

    private sealed record ChunkMeta(string Url, long TotalSize, int ChunkSize, string? Sha1);
}
