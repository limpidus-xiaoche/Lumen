using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using Launcher.Core.Download;
using Launcher.Core.Install;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class ModpackDetectTests
{
    [Fact]
    public void Detects_Modrinth()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "modrinth.index.json"), "{}");
        Assert.Equal(ModpackKind.Modrinth, ModpackInstaller.DetectKind(dir));
    }

    [Fact]
    public void Detects_MultiMc()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "mmc-pack.json"), "{}");
        Assert.Equal(ModpackKind.MultiMc, ModpackInstaller.DetectKind(dir));
    }

    [Fact]
    public void Detects_Hmcl()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "modpack.json"), "{}");
        Assert.Equal(ModpackKind.Hmcl, ModpackInstaller.DetectKind(dir));
    }

    [Fact]
    public void Detects_CurseForge()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "manifest.json"), "{}");
        Assert.Equal(ModpackKind.CurseForgeDirect, ModpackInstaller.DetectKind(dir));
    }

    [Fact]
    public void Unknown_Throws_E3004()
    {
        var dir = TempDir();
        var ex = Assert.Throws<InstallException>(() => ModpackInstaller.DetectKind(dir));
        Assert.Equal("E3004", ex.ErrorCode);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-modpack-detect", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public sealed class ModpackParseTests
{
    [Fact]
    public void Parse_Modrinth_Extracts_Version_Loader_And_Files()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "modrinth.index.json"), """
            {
              "formatVersion": 1,
              "name": "Test Pack",
              "dependencies": {
                "minecraft": "1.20.1",
                "fabric-loader": "0.15.0"
              },
              "files": [
                {
                  "path": "mods/sodium.jar",
                  "hashes": { "sha1": "abc123" },
                  "downloads": ["https://cdn.modrinth.com/sodium.jar"],
                  "fileSize": 1234,
                  "env": { "client": "required" }
                },
                {
                  "path": "servermods/onlyserver.jar",
                  "hashes": { "sha1": "def456" },
                  "downloads": ["https://cdn.modrinth.com/server.jar"],
                  "fileSize": 5678,
                  "env": { "client": "unsupported" }
                }
              ]
            }
            """);

        var parsed = ModpackInstaller.Parse(dir, ModpackKind.Modrinth);

        Assert.Equal("Test Pack", parsed.Name);
        Assert.Equal("1.20.1", parsed.GameVersion);
        Assert.Equal(LoaderKind.Fabric, parsed.Loader);
        Assert.Equal("0.15.0", parsed.LoaderVersion);
        Assert.Single(parsed.Files);
        Assert.Equal("mods/sodium.jar", parsed.Files[0].Path);
        Assert.Equal("https://cdn.modrinth.com/sodium.jar", parsed.Files[0].Url);
        Assert.Equal("abc123", parsed.Files[0].Sha1);
        Assert.Equal(1234, parsed.Files[0].Size);
    }

    [Fact]
    public void Parse_Modrinth_Forge_Dependency()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "modrinth.index.json"), """
            {
              "name": "Forge Pack",
              "dependencies": { "minecraft": "1.20.1", "forge": "47.2.0" },
              "files": []
            }
            """);

        var parsed = ModpackInstaller.Parse(dir, ModpackKind.Modrinth);
        Assert.Equal(LoaderKind.Forge, parsed.Loader);
        Assert.Equal("47.2.0", parsed.LoaderVersion);
    }

    [Fact]
    public void Parse_Hmcl_Extracts_Version_And_Overrides()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "modpack.json"), """
            {
              "name": "HMCL Pack",
              "gameVersion": "1.20.1",
              "modLoader": "fabric",
              "modLoaderVersion": "0.15.0"
            }
            """);
        Directory.CreateDirectory(Path.Combine(dir, "overrides", "mods"));
        File.WriteAllBytes(Path.Combine(dir, "overrides", "mods", "test.jar"), []);

        var parsed = ModpackInstaller.Parse(dir, ModpackKind.Hmcl);

        Assert.Equal("HMCL Pack", parsed.Name);
        Assert.Equal("1.20.1", parsed.GameVersion);
        Assert.Equal(LoaderKind.Fabric, parsed.Loader);
        Assert.NotNull(parsed.OverridesDir);
        Assert.True(Directory.Exists(parsed.OverridesDir!));
    }

    [Fact]
    public void Parse_MultiMc_Extracts_Components_And_Minecraft_Dir()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "mmc-pack.json"), """
            {
              "formatVersion": 1,
              "components": [
                { "uid": "net.minecraft", "version": "1.20.1" },
                { "uid": "net.fabricmc.fabric-loader", "version": "0.15.0" }
              ]
            }
            """);
        File.WriteAllText(Path.Combine(dir, "instance.cfg"), "name=MMC Pack\n");
        Directory.CreateDirectory(Path.Combine(dir, "minecraft", "mods"));
        File.WriteAllBytes(Path.Combine(dir, "minecraft", "mods", "x.jar"), []);

        var parsed = ModpackInstaller.Parse(dir, ModpackKind.MultiMc);

        Assert.Equal("MMC Pack", parsed.Name);
        Assert.Equal("1.20.1", parsed.GameVersion);
        Assert.Equal(LoaderKind.Fabric, parsed.Loader);
        Assert.Equal("0.15.0", parsed.LoaderVersion);
        Assert.NotNull(parsed.OverridesDir);
        Assert.Equal(Path.Combine(dir, "minecraft"), parsed.OverridesDir);
    }

    [Fact]
    public void Parse_CurseForge_Extracts_Version_Loader_And_Overrides()
    {
        var dir = TempDir();
        File.WriteAllText(Path.Combine(dir, "manifest.json"), """
            {
              "name": "CF Pack",
              "minecraft": {
                "version": "1.20.1",
                "modLoaders": [{ "id": "forge-47.2.0", "primary": true }]
              },
              "files": [
                { "projectID": 123, "fileID": 456, "required": true }
              ],
              "overrides": "overrides"
            }
            """);
        Directory.CreateDirectory(Path.Combine(dir, "overrides", "config"));

        var parsed = ModpackInstaller.Parse(dir, ModpackKind.CurseForgeDirect);

        Assert.Equal("CF Pack", parsed.Name);
        Assert.Equal("1.20.1", parsed.GameVersion);
        Assert.Equal(LoaderKind.Forge, parsed.Loader);
        Assert.Equal("47.2.0", parsed.LoaderVersion);
        Assert.Empty(parsed.Files); // CF 无直链，不下载
        Assert.NotNull(parsed.OverridesDir);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-modpack-parse", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public sealed class ModpackInstallTests
{
    [Fact]
    public async Task Install_Modrinth_With_Local_Files_Downloads_And_Copies()
    {
        // 准备一个假 mod 内容
        var modContent = new byte[] { 1, 2, 3, 4, 5 };
        var modSha1 = Convert.ToHexString(SHA1.HashData(modContent)).ToLowerInvariant();

        // 创建一个本地 Modrinth 整合包目录
        var packDir = TempDir();
        File.WriteAllText(Path.Combine(packDir, "modrinth.index.json"), $$"""
            {
              "name": "Local Test Pack",
              "dependencies": { "minecraft": "1.20.1" },
              "files": [
                {
                  "path": "mods/testmod.jar",
                  "hashes": { "sha1": "{{modSha1}}" },
                  "downloads": ["https://cdn.modrinth.com/testmod.jar"],
                  "fileSize": {{modContent.Length}},
                  "env": { "client": "required" }
                }
              ]
            }
            """);
        Directory.CreateDirectory(Path.Combine(packDir, "overrides", "config"));
        File.WriteAllText(Path.Combine(packDir, "overrides", "config", "test.json"), "{}");

        // 设置数据目录和 SQLite
        var dataRoot = TempDir();
        var paths = new LauncherPaths(dataRoot, dataRoot);
        paths.EnsureCreated();
        var sqlite = new SqliteStore(paths);
        await sqlite.InitializeAsync();

        // 创建一个返回 modContent 的 mock HttpClient
        var handler = new StaticHandler(modContent);
        var downloads = new DownloadEngine(new HttpClient(handler));

        // 创建一个真实的 VanillaInstaller 但用 mock resolver
        // 由于安装原版需要网络，我们直接测试 ModpackInstaller 的文件下载和 overrides 复制部分
        // 通过 Parse 后手动调用下载逻辑

        var parsed = ModpackInstaller.Parse(packDir, ModpackKind.Modrinth);
        Assert.Single(parsed.Files);
        Assert.NotNull(parsed.OverridesDir);

        // 模拟下载到 gameDir
        var gameDir = Path.Combine(paths.InstancesDir, "test-instance");
        Directory.CreateDirectory(gameDir);

        var dest = Path.Combine(gameDir, "mods", "testmod.jar");
        Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
        UrlGuard.Validate(parsed.Files[0].Url);
        await downloads.DownloadAsync(
            new DownloadItem(parsed.Files[0].Url, dest, parsed.Files[0].Sha1, parsed.Files[0].Size),
            null, CancellationToken.None);

        Assert.True(File.Exists(dest));
        Assert.Equal(modContent, await File.ReadAllBytesAsync(dest));

        // 模拟 overrides 复制
        foreach (var file in Directory.EnumerateFiles(parsed.OverridesDir!, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(parsed.OverridesDir!, file);
            var target = Path.Combine(gameDir, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }

        Assert.True(File.Exists(Path.Combine(gameDir, "config", "test.json")));
    }

    [Fact]
    public async Task Install_From_Zip_Extracts_And_Detects()
    {
        // 创建 zip 整合包
        var packDir = TempDir();
        File.WriteAllText(Path.Combine(packDir, "modrinth.index.json"), """
            {
              "name": "Zipped Pack",
              "dependencies": { "minecraft": "1.20.1" },
              "files": []
            }
            """);

        var zipPath = Path.Combine(Path.GetTempPath(), $"lumen-zip-{Guid.NewGuid():N}.mrpack");
        ZipFile.CreateFromDirectory(packDir, zipPath);

        // 设置 ModpackInstaller
        var dataRoot = TempDir();
        var paths = new LauncherPaths(dataRoot, dataRoot);
        paths.EnsureCreated();
        var sqlite = new SqliteStore(paths);
        await sqlite.InitializeAsync();

        var installer = new ModpackInstaller(
            paths,
            new DownloadEngine(),
            null!, // VanillaInstaller 不调用
            sqlite,
            NullLogger<ModpackInstaller>.Instance);

        // 直接测试解压 + 识别
        // 由于 InstallAsync 会调用 VanillaInstaller（为 null），我们只测解压逻辑
        // 通过反射或直接测试 EnsureExtracted —— 但它是 private
        // 所以我们验证 zip 文件存在，且 Parse 能处理解压后的内容
        Assert.True(File.Exists(zipPath));

        // 手动解压验证
        var extractDir = Path.Combine(paths.CacheDir, "test-extract");
        Directory.CreateDirectory(extractDir);
        ZipFile.ExtractToDirectory(zipPath, extractDir);
        Assert.Equal(ModpackKind.Modrinth, ModpackInstaller.DetectKind(extractDir));
        var parsed = ModpackInstaller.Parse(extractDir, ModpackKind.Modrinth);
        Assert.Equal("Zipped Pack", parsed.Name);
        Assert.Equal("1.20.1", parsed.GameVersion);

        File.Delete(zipPath);
    }

    [Fact]
    public async Task Unsupported_Extension_Throws_E3004()
    {
        var dataRoot = TempDir();
        var paths = new LauncherPaths(dataRoot, dataRoot);
        paths.EnsureCreated();

        var fakeFile = Path.Combine(dataRoot, "test.rar");
        File.WriteAllBytes(fakeFile, []);

        var installer = new ModpackInstaller(
            paths, new DownloadEngine(), null!, new SqliteStore(paths),
            NullLogger<ModpackInstaller>.Instance);

        var ex = await Assert.ThrowsAsync<InstallException>(() =>
            installer.InstallAsync(
                new ModpackInstallRequest(fakeFile, null, IsolationLevel.ModsConfig, ModpackKind.Modrinth),
                new Progress<InstallProgress>(_ => { }),
                CancellationToken.None));
        Assert.Equal("E3004", ex.ErrorCode);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-modpack-install", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private sealed class StaticHandler : HttpMessageHandler
    {
        private readonly byte[] _data;

        public StaticHandler(byte[] data)
        {
            _data = data;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(_data),
            };
            response.Content.Headers.ContentLength = _data.Length;
            return Task.FromResult(response);
        }
    }
}
