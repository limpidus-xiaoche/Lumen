using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Launcher.Core.Download;
using Launcher.Core.Java;
using Launcher.Core.Launch;
using Launcher.Core.Storage;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class JavaRuntimeInstallerTests
{
    private const string IndexUrl =
        "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private const string ManifestBase =
        "https://launchermeta.mojang.com/v1/products/java-runtime/2ec0cc96c44e5a76b9c8b7c39df7210883d12871";

    [Theory]
    [InlineData(8, "jre-legacy")]
    [InlineData(16, "java-runtime-alpha")]
    [InlineData(17, "java-runtime-beta")]
    [InlineData(21, "java-runtime-gamma")]
    public void SelectComponent_Maps_Known_Majors(int major, string expected) =>
        Assert.Equal(expected, JavaRuntimeInstaller.SelectComponent(null, major));

    [Fact]
    public void SelectComponent_Prefers_Explicit_Component() =>
        Assert.Equal("custom-runtime", JavaRuntimeInstaller.SelectComponent(" custom-runtime ", 8));

    [Fact]
    public void SelectComponent_Unknown_Major_Throws_E4002()
    {
        var ex = Assert.Throws<LaunchException>(
            () => JavaRuntimeInstaller.SelectComponent(null, 22));
        Assert.Equal("E4002", ex.ErrorCode);
    }

    [Fact]
    public async Task EnsureAsync_Downloads_Verifies_And_Reuses_Cache()
    {
        const string component = "java-runtime-gamma";
        var javaBytes = Encoding.UTF8.GetBytes("fake java executable");
        var releaseBytes = Encoding.UTF8.GetBytes("JAVA_VERSION=\"21.0.1\"\nOS_ARCH=\"x86_64\"\n");
        var (indexJson, manifestJson) = BuildIndexAndManifest(
            component,
            new Dictionary<string, object?>
            {
                ["bin"] = new Dictionary<string, object?> { ["type"] = "directory" },
                ["bin/java.exe"] =
                    FileEntry($"{ManifestBase}/files/java.exe", javaBytes),
                ["release"] =
                    FileEntry($"{ManifestBase}/files/release", releaseBytes),
            });

        var handler = new FakeHandler(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [IndexUrl] = Encoding.UTF8.GetBytes(indexJson),
            [$"{ManifestBase}/runtimes/{component}.json"] = Encoding.UTF8.GetBytes(manifestJson),
            [$"{ManifestBase}/files/java.exe"] = javaBytes,
            [$"{ManifestBase}/files/release"] = releaseBytes,
        });

        var root = TempDir();
        try
        {
            var paths = new LauncherPaths(root, root);
            var installer = new JavaRuntimeInstaller(
                paths, new DownloadEngine(new HttpClient(handler)));

            var runtime = await installer.EnsureAsync(
                null, 21, null, CancellationToken.None);

            Assert.Equal(21, runtime.MajorVersion);
            Assert.True(runtime.Is64Bit);

            var exe = Path.Combine(
                paths.SharedDir, "runtimes", component, "bin", "java.exe");
            Assert.True(File.Exists(exe));
            Assert.Equal(javaBytes, await File.ReadAllBytesAsync(exe));

            var requestsAfterFirstInstall = handler.RequestCount;
            var cached = await installer.EnsureAsync(
                null, 21, null, CancellationToken.None);
            Assert.Equal(21, cached.MajorVersion);
            Assert.Equal(requestsAfterFirstInstall, handler.RequestCount);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureAsync_Fails_When_Sha1_Mismatch()
    {
        const string component = "java-runtime-gamma";
        var javaBytes = Encoding.UTF8.GetBytes("fake java executable");
        var (indexJson, manifestJson) = BuildIndexAndManifest(
            component,
            new Dictionary<string, object?>
            {
                ["bin/java.exe"] = new Dictionary<string, object?>
                {
                    ["type"] = "file",
                    ["downloads"] = new Dictionary<string, object?>
                    {
                        ["raw"] = new Dictionary<string, object?>
                        {
                            ["url"] = $"{ManifestBase}/files/java.exe",
                            ["sha1"] = new string('0', 40),
                            ["size"] = javaBytes.Length,
                        },
                    },
                },
            });

        var handler = new FakeHandler(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [IndexUrl] = Encoding.UTF8.GetBytes(indexJson),
            [$"{ManifestBase}/runtimes/{component}.json"] = Encoding.UTF8.GetBytes(manifestJson),
            [$"{ManifestBase}/files/java.exe"] = javaBytes,
        });

        var root = TempDir();
        try
        {
            var paths = new LauncherPaths(root, root);
            var installer = new JavaRuntimeInstaller(
                paths, new DownloadEngine(new HttpClient(handler)));

            var ex = await Assert.ThrowsAsync<LaunchException>(() =>
                installer.EnsureAsync(null, 21, null, CancellationToken.None));

            Assert.Equal("E4001", ex.ErrorCode);
            Assert.False(File.Exists(Path.Combine(
                paths.SharedDir, "runtimes", component, "bin", "java.exe")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task EnsureAsync_Rejects_Path_Escape_From_Manifest()
    {
        const string component = "java-runtime-gamma";
        var payload = Encoding.UTF8.GetBytes("evil");
        var (indexJson, manifestJson) = BuildIndexAndManifest(
            component,
            new Dictionary<string, object?>
            {
                ["../../escape"] = FileEntry($"{ManifestBase}/files/evil", payload),
            });

        var handler = new FakeHandler(new Dictionary<string, byte[]>(StringComparer.Ordinal)
        {
            [IndexUrl] = Encoding.UTF8.GetBytes(indexJson),
            [$"{ManifestBase}/runtimes/{component}.json"] = Encoding.UTF8.GetBytes(manifestJson),
            [$"{ManifestBase}/files/evil"] = payload,
        });

        var root = TempDir();
        try
        {
            var paths = new LauncherPaths(root, root);
            var installer = new JavaRuntimeInstaller(
                paths, new DownloadEngine(new HttpClient(handler)));

            var ex = await Assert.ThrowsAsync<LaunchException>(() =>
                installer.EnsureAsync(null, 21, null, CancellationToken.None));

            Assert.Equal("E4002", ex.ErrorCode);
            Assert.Contains("越界", ex.Message);
            Assert.False(Directory.Exists(Path.Combine(paths.SharedDir, "escape")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static (string IndexJson, string ManifestJson) BuildIndexAndManifest(
        string component,
        Dictionary<string, object?> files)
    {
        var manifestJson = JsonSerializer.Serialize(
            new Dictionary<string, object?> { ["files"] = files });
        var manifestBytes = Encoding.UTF8.GetBytes(manifestJson);

        var indexJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["windows-x64"] = new Dictionary<string, object?>
            {
                [component] = new object[]
                {
                    new Dictionary<string, object?>
                    {
                        ["manifest"] = new Dictionary<string, object?>
                        {
                            ["url"] = $"{ManifestBase}/runtimes/{component}.json",
                            ["sha1"] = Sha1(manifestBytes),
                            ["size"] = manifestBytes.Length,
                        },
                    },
                },
            },
        });

        return (indexJson, manifestJson);
    }

    private static Dictionary<string, object?> FileEntry(string url, byte[] data) =>
        new()
        {
            ["type"] = "file",
            ["downloads"] = new Dictionary<string, object?>
            {
                ["raw"] = new Dictionary<string, object?>
                {
                    ["url"] = url,
                    ["sha1"] = Sha1(data),
                    ["size"] = data.Length,
                },
            },
        };

    private static string Sha1(byte[] data) =>
        Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();

    private static string TempDir()
    {
        var dir = Path.Combine(
            Path.GetTempPath(), "lumen-java-runtime", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, byte[]> _routes;

        public FakeHandler(Dictionary<string, byte[]> routes)
        {
            _routes = routes;
        }

        public int RequestCount { get; private set; }

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            var url = request.RequestUri!.ToString();
            Requests.Add(url);

            if (!_routes.TryGetValue(url, out var data))
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            }

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(data),
            };
            response.Content.Headers.ContentLength = data.Length;
            return Task.FromResult(response);
        }
    }
}
