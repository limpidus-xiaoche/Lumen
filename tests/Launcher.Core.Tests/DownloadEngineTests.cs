using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using Launcher.Core.Download;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class DownloadEngineTests
{
    [Fact]
    public async Task Small_File_Downloads_And_Verifies()
    {
        var data = RandomNumberGenerator.GetBytes(4096);
        var engine = new DownloadEngine(new HttpClient(new RangeHandler(data)));
        var dir = TempDir();
        var dest = Path.Combine(dir, "small.bin");

        await engine.DownloadAsync(
            new DownloadItem("https://piston-meta.mojang.com/small.bin", dest, Sha1(data), data.Length),
            null, CancellationToken.None);

        Assert.True(File.Exists(dest));
        Assert.Equal(data, await File.ReadAllBytesAsync(dest));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task Hash_Mismatch_Throws_E2003()
    {
        var data = RandomNumberGenerator.GetBytes(1024);
        var engine = new DownloadEngine(new HttpClient(new RangeHandler(data)));
        var dir = TempDir();
        var dest = Path.Combine(dir, "bad.bin");

        var ex = await Assert.ThrowsAsync<DownloadException>(() =>
            engine.DownloadAsync(
                new DownloadItem("https://piston-meta.mojang.com/bad.bin", dest, new string('0', 40), data.Length),
                null, CancellationToken.None));

        Assert.Equal("E2003", ex.ErrorCode);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public async Task Large_File_Uses_Chunks_And_Range()
    {
        var data = RandomNumberGenerator.GetBytes(17 * 1024 * 1024 + 123);
        var handler = new RangeHandler(data);
        var engine = new DownloadEngine(new HttpClient(handler));
        var dir = TempDir();
        var dest = Path.Combine(dir, "large.bin");

        await engine.DownloadAsync(
            new DownloadItem("https://piston-meta.mojang.com/large.bin", dest, Sha1(data), data.Length),
            null, CancellationToken.None);

        Assert.True(File.Exists(dest));
        Assert.Equal(data.Length, new FileInfo(dest).Length);
        Assert.True(handler.RangeRequests > 0);

        Directory.Delete(dir, recursive: true);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-download", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string Sha1(byte[] data) =>
        Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();

    private sealed class RangeHandler : HttpMessageHandler
    {
        private readonly byte[] _data;

        public RangeHandler(byte[] data)
        {
            _data = data;
        }

        public int RangeRequests { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var range = request.Headers.Range?.Ranges.FirstOrDefault();
            if (range is not null)
            {
                RangeRequests++;
                var start = (int)(range.From ?? 0);
                var end = (int)Math.Min(range.To ?? _data.Length - 1, _data.Length - 1);
                var slice = _data[start..(end + 1)];
                var partial = new HttpResponseMessage(HttpStatusCode.PartialContent)
                {
                    Content = new ByteArrayContent(slice),
                };
                partial.Content.Headers.ContentLength = slice.Length;
                return Task.FromResult(partial);
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_data),
            };
            response.Content.Headers.ContentLength = _data.Length;
            return Task.FromResult(response);
        }
    }
}
