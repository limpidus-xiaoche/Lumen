using System.Net;
using Launcher.Core.Install;
using Launcher.Core.Launch;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class LoaderIdsTests
{
    [Fact]
    public void Builds_Canonical_Ids()
    {
        Assert.Equal("1.21.1", LoaderIds.EffectiveVersionId("1.21.1", LoaderKind.Vanilla, null));
        Assert.Equal("fabric-loader-0.16.9-1.21.1",
            LoaderIds.EffectiveVersionId("1.21.1", LoaderKind.Fabric, "0.16.9"));
        Assert.Equal("1.20.1-forge-47.4.28",
            LoaderIds.EffectiveVersionId("1.20.1", LoaderKind.Forge, "47.4.28"));
        Assert.Equal("1.21.1-neoforge-21.1.72",
            LoaderIds.EffectiveVersionId("1.21.1", LoaderKind.NeoForge, "21.1.72"));
    }

    [Fact]
    public void Missing_Loader_Version_Throws_E3001()
    {
        var ex = Assert.Throws<InstallException>(() =>
            LoaderIds.EffectiveVersionId("1.21.1", LoaderKind.Fabric, " "));
        Assert.Equal("E3001", ex.ErrorCode);
    }
}

public sealed class LoaderCatalogTests
{
    [Fact]
    public void Fabric_Versions_Are_Parsed()
    {
        var versions = LoaderCatalog.ParseFabricVersions("""
            [
              { "loader": { "version": "0.16.9", "stable": true } },
              { "loader": { "version": "0.17.0-beta.1", "stable": false } }
            ]
            """);

        Assert.Equal(2, versions.Count);
        Assert.Equal("0.16.9", versions[0].Version);
        Assert.True(versions[0].Stable);
        Assert.False(versions[1].Stable);
    }

    [Fact]
    public void Forge_Versions_Are_Filtered_And_Sorted()
    {
        const string xml = """
            <metadata><versioning><versions>
              <version>1.20.1-47.4.27</version>
              <version>1.20.1-47.4.28</version>
              <version>1.19.2-43.3.0</version>
            </versions></versioning></metadata>
            """;

        var versions = LoaderCatalog.ParseForgeVersions(xml, "1.20.1", "47.4.28");

        Assert.Equal(2, versions.Count);
        Assert.Equal("47.4.28", versions[0].Version);
        Assert.True(versions[0].Stable);
        Assert.False(versions[1].Stable);
    }

    [Fact]
    public void NeoForge_Versions_Map_To_Game_Version()
    {
        const string xml = """
            <metadata><versioning><versions>
              <version>21.1.72</version>
              <version>21.1.10</version>
              <version>21.0.167</version>
              <version>20.4.237</version>
            </versions></versioning></metadata>
            """;

        var versions = LoaderCatalog.ParseNeoForgeVersions(xml, "1.21.1");

        Assert.Equal(2, versions.Count);
        Assert.Equal("21.1.72", versions[0].Version);
        Assert.Equal("21.1.10", versions[1].Version);
        Assert.Equal("21.1", LoaderCatalog.MapNeoForgePrefix("1.21.1"));
        Assert.Equal("21.0", LoaderCatalog.MapNeoForgePrefix("1.21"));
        Assert.Null(LoaderCatalog.MapNeoForgePrefix("1.20.1"));
    }

    [Fact]
    public void Versions_Compare_Numerically()
    {
        Assert.True(LoaderCatalog.CompareVersions("47.4.28", "47.4.27") > 0);
        Assert.True(LoaderCatalog.CompareVersions("0.16.9", "0.16.10") < 0);
        Assert.True(LoaderCatalog.CompareVersions("21.1.72", "21.1.10") > 0);
        Assert.Equal(0, LoaderCatalog.CompareVersions("1.0.0", "1.0.0"));
    }

    [Fact]
    public void Loader_Candidates_Official_Excludes_Bmclapi()
    {
        const string forgeMetadata =
            "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
        var forgeInstaller = LoaderCatalog.ForgeInstallerUrl("1.20.1", "47.4.28");
        var neoForgeInstaller = LoaderCatalog.NeoForgeInstallerUrl("21.1.72");

        Assert.Equal(new[] { forgeMetadata },
            LoaderCatalog.BuildLoaderCandidates(forgeMetadata, DownloadSourceMode.Official));
        Assert.Equal(new[] { forgeInstaller },
            LoaderCatalog.BuildLoaderCandidates(forgeInstaller, DownloadSourceMode.Official));
        Assert.Equal(new[] { neoForgeInstaller },
            LoaderCatalog.BuildLoaderCandidates(neoForgeInstaller, DownloadSourceMode.Official));
    }

    [Fact]
    public void Loader_Candidates_Mirror_Prefers_Bmclapi()
    {
        const string forgeMetadata =
            "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
        var forgeInstaller = LoaderCatalog.ForgeInstallerUrl("1.20.1", "47.4.28");
        var neoForgeInstaller = LoaderCatalog.NeoForgeInstallerUrl("21.1.72");

        var forge = LoaderCatalog.BuildLoaderCandidates(forgeMetadata, DownloadSourceMode.Mirror);
        Assert.Equal(2, forge.Count);
        Assert.StartsWith("https://bmclapi2.bangbang93.com/maven", forge[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(forgeMetadata, forge[1]);

        var installer = LoaderCatalog.BuildLoaderCandidates(forgeInstaller, DownloadSourceMode.Mirror);
        Assert.Equal(
            "https://bmclapi2.bangbang93.com/maven/net/minecraftforge/forge/1.20.1-47.4.28/" +
            "forge-1.20.1-47.4.28-installer.jar",
            installer[0]);
        Assert.Equal(forgeInstaller, installer[1]);

        var neo = LoaderCatalog.BuildLoaderCandidates(neoForgeInstaller, DownloadSourceMode.Mirror);
        Assert.StartsWith("https://bmclapi2.bangbang93.com/maven", neo[0], StringComparison.OrdinalIgnoreCase);
        Assert.Equal(neoForgeInstaller, neo[1]);
    }

    [Fact]
    public void Loader_Candidates_Auto_Keeps_Official_First_And_Fabric_Unchanged()
    {
        const string forgeMetadata =
            "https://maven.minecraftforge.net/net/minecraftforge/forge/maven-metadata.xml";
        var auto = LoaderCatalog.BuildLoaderCandidates(forgeMetadata, DownloadSourceMode.Auto);
        Assert.Equal(2, auto.Count);
        Assert.Equal(forgeMetadata, auto[0]);
        Assert.StartsWith("https://bmclapi2.bangbang93.com/maven", auto[1], StringComparison.OrdinalIgnoreCase);

        const string fabric = "https://meta.fabricmc.net/v2/versions/loader/1.20.1";
        Assert.Equal(new[] { fabric },
            LoaderCatalog.BuildLoaderCandidates(fabric, DownloadSourceMode.Mirror));
    }

    [Fact]
    public async Task LoaderCatalog_Official_Mode_Does_Not_Request_Bmclapi()
    {
        const string xml = """
            <metadata><versioning><versions>
              <version>1.20.1-47.4.28</version>
            </versions></versioning></metadata>
            """;
        var handler = new RecordingHandler(uri =>
            uri.Host.Contains("minecraftforge", StringComparison.OrdinalIgnoreCase)
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(xml) }
                : null);
        var catalog = new LoaderCatalog(new HttpClient(handler), () => DownloadSourceMode.Official);

        var versions = await catalog.GetVersionsAsync(LoaderKind.Forge, "1.20.1", CancellationToken.None);

        Assert.Equal("47.4.28", Assert.Single(versions).Version);
        Assert.DoesNotContain(handler.Requests, url =>
            url.Contains("bmclapi", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class RecordingHandler : HttpMessageHandler
    {
        private readonly Func<Uri, HttpResponseMessage?> _respond;

        public RecordingHandler(Func<Uri, HttpResponseMessage?> respond) => _respond = respond;

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

public sealed class VersionDetailParsingTests
{
    [Fact]
    public void Empty_ReleaseTime_Is_Treated_As_Null()
    {
        var detail = ManifestClient.DeserializeDetail("""
            { "id": "fabric-loader-0.20.1-1.21.1", "inheritsFrom": "1.21.1",
              "releaseTime": "", "libraries": [] }
            """);

        Assert.Null(detail.ReleaseTime);
        Assert.Equal("1.21.1", detail.InheritsFrom);
    }
}

public sealed class VersionResolverLocalTests
{
    [Fact]
    public async Task Resolves_Local_Loader_Version_Without_Network()
    {
        var root = TempRoot();
        var paths = new LauncherPaths(root, root);
        WriteVersion(paths, "1.21.1", """
            {
              "id": "1.21.1",
              "mainClass": "net.minecraft.client.main.Main",
              "assetIndex": { "id": "17", "url": "https://piston-meta.mojang.com/a.json" },
              "libraries": [
                { "name": "com.example:base:1.0",
                  "downloads": { "artifact": { "path": "com/example/base/1.0/base-1.0.jar" } } }
              ],
              "arguments": { "game": ["--username", "${auth_player_name}"], "jvm": ["-cp", "${classpath}"] }
            }
            """);
        WriteVersion(paths, "fabric-loader-0.16.9-1.21.1", """
            {
              "id": "fabric-loader-0.16.9-1.21.1",
              "inheritsFrom": "1.21.1",
              "mainClass": "net.fabricmc.loader.impl.launch.knot.KnotClient",
              "libraries": [
                { "name": "net.fabricmc:fabric-loader:0.16.9",
                  "downloads": { "artifact": { "path": "net/fabricmc/fabric-loader/0.16.9/fabric-loader-0.16.9.jar" } } }
              ],
              "arguments": { "jvm": ["-DFabricMcEmu=net.minecraft.client.main.Main"], "game": [] }
            }
            """);

        var client = new ManifestClient(paths, new HttpClient(new FailingHandler()));
        var resolver = new VersionResolver(client, paths);
        var detail = await resolver.ResolveAsync("fabric-loader-0.16.9-1.21.1");

        Assert.Equal("net.fabricmc.loader.impl.launch.knot.KnotClient", detail.MainClass);
        Assert.Equal(2, detail.Libraries.Count);
        Assert.NotNull(detail.AssetIndex);
        Assert.Equal("17", detail.AssetIndex.Id);
        Assert.Contains(detail.Arguments!.Game, e => e.GetString() == "--username");
        Assert.Contains(detail.Arguments.Jvm, e => e.GetString()?.Contains("FabricMcEmu") == true);
    }

    private static void WriteVersion(LauncherPaths paths, string id, string json)
    {
        var dir = Path.Combine(paths.SharedDir, "versions", id);
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, $"{id}.json"), json);
    }

    private static string TempRoot()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-loader", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            throw new HttpRequestException("本地解析不应访问网络");
    }
}

public sealed class LoaderInstallerJavaFallbackTests
{
    [Fact]
    public async Task Missing_Java_Invokes_Installer_And_Retries()
    {
        var runtime = new JavaRuntime(@"C:\fake\java.exe", 21, true, "Test", "21");
        var selectAttempts = 0;
        var ensureCalls = 0;
        var messages = new List<string>();

        var selected = await LoaderInstaller.SelectJavaWithFallbackAsync(
            () =>
            {
                selectAttempts++;
                if (selectAttempts == 1)
                {
                    throw new LaunchException("E4001", "未找到满足 Java 21 的运行环境");
                }

                return runtime;
            },
            new VersionDetail { Id = "1.21.1" },
            (major, _, _) =>
            {
                ensureCalls++;
                Assert.Equal(21, major);
                return Task.FromResult(runtime);
            },
            messages.Add,
            CancellationToken.None);

        Assert.Equal(2, selectAttempts);
        Assert.Equal(1, ensureCalls);
        Assert.Equal(runtime, selected);
        Assert.Contains(messages, m => m.Contains("未找到 Java 21", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Without_Installer_E4001_Is_Propagated()
    {
        var ex = await Assert.ThrowsAsync<LaunchException>(() =>
            LoaderInstaller.SelectJavaWithFallbackAsync(
                () => throw new LaunchException("E4001", "未找到满足 Java 21 的运行环境"),
                new VersionDetail { Id = "1.21.1" },
                ensureRuntime: null,
                report: null,
                ct: CancellationToken.None));

        Assert.Equal("E4001", ex.ErrorCode);
    }

    [Fact]
    public async Task Retry_Failure_Is_Propagated()
    {
        var selectAttempts = 0;
        var runtime = new JavaRuntime(@"C:\fake\java.exe", 21, true, "Test", "21");

        var ex = await Assert.ThrowsAsync<LaunchException>(() =>
            LoaderInstaller.SelectJavaWithFallbackAsync(
                () =>
                {
                    selectAttempts++;
                    throw new LaunchException("E4001", "仍然没有 Java");
                },
                new VersionDetail { Id = "1.21.1" },
                (_, _, _) => Task.FromResult(runtime),
                report: null,
                ct: CancellationToken.None));

        Assert.Equal("E4001", ex.ErrorCode);
        Assert.Equal(2, selectAttempts);
    }
}
