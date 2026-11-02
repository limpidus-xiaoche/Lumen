using System.Text.Json;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class VersionResolverTests
{
    [Fact]
    public void Merge_Child_Overrides_Library_And_Appends_Arguments()
    {
        var parent = new VersionDetail
        {
            Id = "1.20.1",
            MainClass = "net.minecraft.client.main.Main",
            Libraries =
            [
                new Library { Name = "com.example:parent-lib:1.0" },
                new Library { Name = "com.example:shared-lib:1.0" },
            ],
            Arguments = new VersionArguments
            {
                Game = [JsonDocument.Parse("\"--parent\"").RootElement],
                Jvm = [JsonDocument.Parse("\"-Xmx1G\"").RootElement],
            },
            JavaVersion = new JavaVersionRef { MajorVersion = 17 },
        };

        var child = new VersionDetail
        {
            Id = "1.20.1-forge-47.2.0",
            InheritsFrom = "1.20.1",
            MainClass = "cpw.mods.bootstraplauncher.BootstrapLauncher",
            Libraries =
            [
                new Library { Name = "com.example:shared-lib:2.0" },
                new Library { Name = "com.example:child-lib:1.0" },
            ],
            Arguments = new VersionArguments
            {
                Game = [JsonDocument.Parse("\"--child\"").RootElement],
            },
        };

        var merged = VersionResolver.Merge(parent, child);

        Assert.Equal("1.20.1-forge-47.2.0", merged.Id);
        Assert.Null(merged.InheritsFrom);
        Assert.Equal("cpw.mods.bootstraplauncher.BootstrapLauncher", merged.MainClass);
        Assert.Equal(3, merged.Libraries.Count);
        Assert.Equal("com.example:shared-lib:2.0", merged.Libraries.Single(l => l.DedupeKey == "com.example:shared-lib").Name);
        Assert.Equal(2, merged.Arguments!.Game.Count);
        Assert.Single(merged.Arguments.Jvm);
        Assert.Equal(17, merged.JavaVersion!.MajorVersion);
    }

    [Fact]
    public void Manifest_Parses()
    {
        const string json = """
            {
              "latest": { "release": "1.21.1", "snapshot": "26.4-snapshot-5" },
              "versions": [
                { "id": "1.21.1", "type": "release", "url": "https://example.invalid/1.21.1.json", "sha1": "abc" }
              ]
            }
            """;

        var manifest = ManifestClient.ParseManifest(json);
        Assert.Equal("1.21.1", manifest.LatestRelease);
        Assert.Equal("26.4-snapshot-5", manifest.Latest!.Snapshot);
        Assert.Single(manifest.Versions);
        Assert.Equal("1.21.1", manifest.Versions[0].Id);
    }

    [Fact]
    public void Version_Detail_Parses_Legacy_And_Modern_Fields()
    {
        const string json = """
            {
              "id": "1.12.2",
              "mainClass": "net.minecraft.launchwrapper.Launch",
              "minecraftArguments": "--username ${auth_player_name}",
              "javaVersion": { "majorVersion": 8 },
              "libraries": [
                { "name": "com.example:lib:1.0", "rules": [ { "action": "allow", "os": { "name": "windows" } } ] }
              ]
            }
            """;

        var detail = ManifestClient.DeserializeDetail(json);
        Assert.Equal("net.minecraft.launchwrapper.Launch", detail.MainClass);
        Assert.Equal(8, detail.JavaVersion!.MajorVersion);
        Assert.Single(detail.Libraries);
        Assert.Contains("${auth_player_name}", detail.MinecraftArguments);
    }

    [Fact]
    public void Manifest_Candidates_Respect_Source_Mode()
    {
        var auto = ManifestClient.BuildManifestCandidates(DownloadSourceMode.Auto);
        Assert.Equal(2, auto.Count);
        Assert.StartsWith("https://piston-meta.mojang.com", auto[0].ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://bmclapi2.bangbang93.com", auto[1].ToString(), StringComparison.OrdinalIgnoreCase);

        var official = ManifestClient.BuildManifestCandidates(DownloadSourceMode.Official);
        Assert.Single(official);

        var mirror = ManifestClient.BuildManifestCandidates(DownloadSourceMode.Mirror);
        Assert.Equal(2, mirror.Count);
        Assert.StartsWith("https://bmclapi2.bangbang93.com", mirror[0].ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("https://piston-meta.mojang.com", mirror[1].ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Detail_Candidates_Respect_Source_Mode()
    {
        const string url = "https://piston-meta.mojang.com/v1/packages/1.21.1.json";
        const string mirror = "https://bmclapi2.bangbang93.com/v1/packages/1.21.1.json";

        Assert.Equal(new[] { url }, ManifestClient.BuildDetailCandidates(url, DownloadSourceMode.Official));

        var mirrored = ManifestClient.BuildDetailCandidates(url, DownloadSourceMode.Mirror);
        Assert.Equal(new[] { mirror, url }, mirrored);

        var auto = ManifestClient.BuildDetailCandidates(url, DownloadSourceMode.Auto);
        Assert.Equal(new[] { url, mirror }, auto);
    }

    [Fact]
    public async Task Manifest_Official_Mode_Does_Not_Request_Mirror()
    {
        var root = TempRoot();
        var paths = new LauncherPaths(root, root);
        var handler = new RecordingHandler();
        var client = new ManifestClient(
            paths, new HttpClient(handler), () => DownloadSourceMode.Official);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetManifestAsync(force: true));

        Assert.Single(handler.Requests);
        Assert.StartsWith("https://piston-meta.mojang.com", handler.Requests[0], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Manifest_Mirror_Mode_Tries_Mirror_First()
    {
        const string json = """
            {
              "latest": { "release": "1.21.1", "snapshot": "26.4-snapshot-5" },
              "versions": []
            }
            """;
        var root = TempRoot();
        var paths = new LauncherPaths(root, root);
        var handler = new RecordingHandler(uri =>
            uri.Host.Contains("bmclapi", StringComparison.OrdinalIgnoreCase)
                ? new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = new StringContent(json) }
                : null);
        var client = new ManifestClient(
            paths, new HttpClient(handler), () => DownloadSourceMode.Mirror);
        var manifest = await client.GetManifestAsync(force: true);

        Assert.Equal("1.21.1", manifest.LatestRelease);
        Assert.Single(handler.Requests);
        Assert.StartsWith("https://bmclapi2.bangbang93.com", handler.Requests[0], StringComparison.OrdinalIgnoreCase);
    }

    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-manifest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage?> _respond;

        public RecordingHandler(Func<Uri, HttpResponseMessage?>? respond = null) =>
            _respond = respond ?? (_ => null);

        public List<string> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uri = request.RequestUri!;
            Requests.Add(uri.ToString());
            var response = _respond(uri);
            if (response is null)
            {
                throw new HttpRequestException($"请求失败：{uri}");
            }

            return Task.FromResult(response);
        }
    }
}
