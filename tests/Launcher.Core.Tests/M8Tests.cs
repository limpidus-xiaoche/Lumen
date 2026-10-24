using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Launcher.Core.Diagnostics;
using Launcher.Core.Storage;
using Launcher.Core.Update;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class IntegrityServiceTests
{
    [Fact]
    public async Task Full_Passes_For_Intact_Files()
    {
        var fixture = Fixture.Create();
        var service = new IntegrityService(fixture.Paths, appDir: fixture.AppDir, requireManifest: true);

        var report = await service.VerifyAsync(IntegrityCheckMode.Full);

        Assert.True(report.IsValid);
        Assert.Equal(2, report.CheckedFiles);
        Assert.Empty(report.FailedFiles);
    }

    [Fact]
    public async Task Quick_Second_Run_Hits_Cache()
    {
        var fixture = Fixture.Create();
        var service = new IntegrityService(fixture.Paths, appDir: fixture.AppDir, requireManifest: true);

        var first = await service.VerifyAsync(IntegrityCheckMode.Quick);
        var second = await service.VerifyAsync(IntegrityCheckMode.Quick);

        Assert.True(first.IsValid);
        Assert.True(second.IsValid);
        Assert.Contains("缓存", second.Message);
    }

    [Fact]
    public async Task Tampered_Byte_Fails_With_E8002()
    {
        var fixture = Fixture.Create();
        fixture.TamperSameLength("Lumen.dll");
        var service = new IntegrityService(fixture.Paths, appDir: fixture.AppDir, requireManifest: true);

        var ex = await Assert.ThrowsAsync<IntegrityException>(
            () => service.VerifyAsync(IntegrityCheckMode.Full));

        Assert.Equal("E8002", ex.ErrorCode);
        Assert.Contains(ex.FailedFiles, f => f.Contains("Lumen.dll", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Size_Mismatch_Fails()
    {
        var fixture = Fixture.Create();
        File.AppendAllText(Path.Combine(fixture.AppDir, "Lumen.dll"), "x");
        var service = new IntegrityService(fixture.Paths, appDir: fixture.AppDir, requireManifest: true);

        var ex = await Assert.ThrowsAsync<IntegrityException>(
            () => service.VerifyAsync(IntegrityCheckMode.Full));
        Assert.Contains(ex.FailedFiles, f => f.Contains("size", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Missing_Manifest_Fails_When_Required()
    {
        var fixture = Fixture.Create(includeManifest: false);
        var service = new IntegrityService(fixture.Paths, appDir: fixture.AppDir, requireManifest: true);

        var ex = await Assert.ThrowsAsync<IntegrityException>(
            () => service.VerifyAsync(IntegrityCheckMode.Quick));
        Assert.Equal("E8002", ex.ErrorCode);
    }

    [Fact]
    public async Task Missing_Manifest_Skips_When_Optional()
    {
        var fixture = Fixture.Create(includeManifest: false);
        var service = new IntegrityService(fixture.Paths, appDir: fixture.AppDir, requireManifest: false);

        var report = await service.VerifyAsync(IntegrityCheckMode.Quick);
        Assert.True(report.IsValid);
        Assert.Equal(0, report.CheckedFiles);
    }

    [Fact]
    public void Missing_Sha256_Is_Rejected()
    {
        var json = """
                   { "version": "1.0.0", "files": [ { "path": "Lumen.exe", "size": 10 } ] }
                   """;
        var ex = Assert.Throws<IntegrityException>(() => IntegrityManifest.Parse(json));
        Assert.Contains("sha256", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Path_Traversal_Is_Rejected()
    {
        var json = """
                   { "version": "1.0.0", "files": [ { "path": "../evil.dll", "size": 10, "sha256": "0000000000000000000000000000000000000000000000000000000000000000" } ] }
                   """;
        Assert.Throws<IntegrityException>(() => IntegrityManifest.Parse(json));
    }

    private sealed class Fixture
    {
        public string Root { get; private init; } = string.Empty;
        public string AppDir { get; private init; } = string.Empty;
        public LauncherPaths Paths { get; private init; } = null!;

        public static Fixture Create(bool includeManifest = true)
        {
            var root = Path.Combine(Path.GetTempPath(), "lumen-integrity", Guid.NewGuid().ToString("N"));
            var appDir = Path.Combine(root, "app");
            Directory.CreateDirectory(appDir);
            File.WriteAllText(Path.Combine(appDir, "Lumen.exe"), "exe-content");
            File.WriteAllText(Path.Combine(appDir, "Lumen.dll"), "dll-content");

            var paths = new LauncherPaths(Path.Combine(root, "config"), Path.Combine(root, "local"));
            paths.EnsureCreated();

            var fixture = new Fixture { Root = root, AppDir = appDir, Paths = paths };
            if (includeManifest)
            {
                fixture.WriteManifest();
            }

            return fixture;
        }

        public void TamperSameLength(string fileName)
        {
            var path = Path.Combine(AppDir, fileName);
            var text = File.ReadAllText(path);
            var tampered = char.ToUpperInvariant(text[0]) + text[1..];
            File.WriteAllText(path, tampered);
        }

        private void WriteManifest()
        {
            var files = Directory.GetFiles(AppDir)
                .Where(f => !string.Equals(Path.GetFileName(f), "integrity.json", StringComparison.OrdinalIgnoreCase))
                .Select(f => new
                {
                    path = Path.GetFileName(f),
                    size = new FileInfo(f).Length,
                    sha256 = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(f))),
                })
                .ToArray();

            var json = JsonSerializer.Serialize(new { version = "0.8.0", algorithm = "sha256", files });
            File.WriteAllText(Path.Combine(AppDir, "integrity.json"), json);
        }
    }
}

public sealed class UpdateServiceTests
{
    private const string Sha =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    [Fact]
    public void Parser_Accepts_Full_Manifest_And_Ignores_Unknown()
    {
        var json = $$"""
                     {
                       "version": "0.9.0",
                       "minSupported": "0.7.0",
                       "url": "https://github.com/x/Lumen/releases/download/0.9.0/Setup.exe",
                       "sha256": "{{Sha}}",
                       "size": 123456,
                       "publishedAt": "2026-10-24T00:00:00Z",
                       "notes": "修复若干问题",
                       "signature": "future-field"
                     }
                     """;

        var manifest = ReleaseManifestParser.Parse(json);
        Assert.Equal("0.9.0", manifest.Version);
        Assert.Equal("0.7.0", manifest.MinSupported);
        Assert.Equal(123456, manifest.Size);
        Assert.Equal("修复若干问题", manifest.Notes);
    }

    [Fact]
    public void Parser_Rejects_Missing_Sha256()
    {
        var json = """
                   { "version": "0.9.0", "url": "https://github.com/x/Lumen/Setup.exe", "size": 10 }
                   """;
        var ex = Assert.Throws<UpdateException>(() => ReleaseManifestParser.Parse(json));
        Assert.Equal("E8001", ex.ErrorCode);
    }

    [Fact]
    public void Parser_Rejects_Missing_Size()
    {
        var json = $$"""
                     { "version": "0.9.0", "url": "https://github.com/x/Lumen/Setup.exe", "sha256": "{{Sha}}" }
                     """;
        var ex = Assert.Throws<UpdateException>(() => ReleaseManifestParser.Parse(json));
        Assert.Equal("E8001", ex.ErrorCode);
    }

    [Theory]
    [InlineData("1.10.0", "1.9.9", 1)]
    [InlineData("1.0.0", "1.0.0-beta", 1)]
    [InlineData("1.0.0-beta", "1.0.0", -1)]
    [InlineData("v0.8.0", "0.8.0", 0)]
    public void Version_Compare_Is_Monotonic(string left, string right, int expectedSign)
    {
        Assert.Equal(expectedSign, Math.Sign(AppVersion.Compare(left, right)));
    }

    [Fact]
    public void Downgrade_Is_Rejected_Unless_Forced()
    {
        var manifest = new ReleaseManifest("0.7.0", null, "https://github.com/x/Setup.exe", 10, Sha, null, null);

        Assert.Null(UpdateService.SelectUpdate(manifest, "0.8.0"));
        Assert.NotNull(UpdateService.SelectUpdate(manifest, "0.8.0", force: true));
        Assert.Null(UpdateService.SelectUpdate(manifest, "0.7.0"));
        Assert.NotNull(UpdateService.SelectUpdate(manifest, "0.6.0"));
    }

    [Fact]
    public async Task Verifier_Accepts_Intact_Package()
    {
        var path = TempFile("payload");
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var actual = await UpdatePackageVerifier.VerifyFileAsync(
            path, new FileInfo(path).Length, sha);
        Assert.Equal(sha, actual);
    }

    [Fact]
    public async Task Verifier_Rejects_Size_Mismatch_With_E8001()
    {
        var path = TempFile("payload");
        var sha = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(path)));
        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => UpdatePackageVerifier.VerifyFileAsync(path, new FileInfo(path).Length + 1, sha));
        Assert.Equal("E8001", ex.ErrorCode);
    }

    [Fact]
    public async Task Verifier_Rejects_Sha_Mismatch_With_E8001()
    {
        var path = TempFile("payload");
        var ex = await Assert.ThrowsAsync<UpdateException>(
            () => UpdatePackageVerifier.VerifyFileAsync(path, new FileInfo(path).Length, Sha));
        Assert.Equal("E8001", ex.ErrorCode);
    }

    private static string TempFile(string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "lumen-update", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }
}

public sealed class DiagnosticsExporterTests
{
    [Fact]
    public async Task Export_Redacts_Secrets_And_Excludes_Credential_Files()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-diag", Guid.NewGuid().ToString("N"));
        var paths = new LauncherPaths(Path.Combine(root, "config"), Path.Combine(root, "local"));
        paths.EnsureCreated();

        var sqlite = new SqliteStore(paths);
        await sqlite.InitializeAsync();
        var settings = new AppSettingsStore(paths);

        var userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        File.WriteAllText(Path.Combine(paths.LogsDir, "launcher-20261024.log"),
            $"accessToken=super-secret-token path={userProfile}\\foo\r\nAuthorization: Bearer abc.def.ghi\r\n");
        File.WriteAllText(Path.Combine(paths.LocalRoot, "accounts.dat"), "cipher");
        File.WriteAllText(Path.Combine(paths.LocalRoot, "args.txt"), "--accessToken secret");

        var exporter = new DiagnosticsExporter(paths, settings, sqlite);
        var zipPath = await exporter.ExportAsync(instanceId: null);

        Assert.True(File.Exists(zipPath));
        using (var zip = ZipFile.OpenRead(zipPath))
        {
            var names = zip.Entries.Select(e => e.FullName).ToArray();
            Assert.Contains("README.txt", names);
            Assert.Contains("version.json", names);
            Assert.Contains("system.json", names);
            Assert.Contains("settings.json", names);
            Assert.Contains("java.json", names);
            Assert.Contains("instances.json", names);
            Assert.DoesNotContain(names, n => n.Contains("accounts.dat", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, n => n.Contains("args.txt", StringComparison.OrdinalIgnoreCase));
            Assert.DoesNotContain(names, n => n.Contains(".part.meta", StringComparison.OrdinalIgnoreCase));

            var logEntry = zip.GetEntry("logs/launcher-20261024.log");
            Assert.NotNull(logEntry);
            using var reader = new StreamReader(logEntry!.Open());
            var text = await reader.ReadToEndAsync();
            Assert.DoesNotContain("super-secret-token", text);
            Assert.DoesNotContain("abc.def.ghi", text);
            Assert.Contains("***", text);
            if (!string.IsNullOrEmpty(userProfile))
            {
                Assert.DoesNotContain(userProfile, text);
                Assert.Contains("%USERPROFILE%", text);
            }
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }
}

public sealed class LauncherPathsTests
{
    [Fact]
    public void NonPortable_Uses_User_Profile_Roots()
    {
        var paths = LauncherPaths.CreateDefault(portable: false, out var fellBack);
        Assert.False(paths.IsPortable);
        Assert.False(fellBack);
        Assert.Contains("Lumen", paths.ConfigRoot);
    }

    [Fact]
    public void CanWrite_Creates_And_Leaves_No_Probe()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-probe", Guid.NewGuid().ToString("N"));
        Assert.True(LauncherPaths.CanWrite(dir));
        Assert.Empty(Directory.GetFiles(dir));
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Portable_Fallback_Flag_Is_Exposed()
    {
        var paths = new LauncherPaths("c", "l", isPortable: false, portableFellBack: true);
        Assert.True(paths.PortableFellBack);
    }
}
