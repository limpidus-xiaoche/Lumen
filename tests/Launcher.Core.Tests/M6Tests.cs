using System.IO.Compression;
using System.Text;
using Launcher.Core;
using Launcher.Core.Instances;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class ModScannerTests
{
    [Fact]
    public void Scans_Enabled_And_Disabled_Mods()
    {
        var gameDir = TempDir();
        var mods = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(mods);
        WriteFabricJar(Path.Combine(mods, "sodium.jar"), "sodium", "Sodium", "0.5.11");
        File.WriteAllBytes(Path.Combine(mods, "oldmod.jar.disabled"), []);
        File.WriteAllText(Path.Combine(mods, "readme.txt"), "ignored");

        var entries = ModScanner.Scan(gameDir, LoaderKind.Fabric);

        Assert.Equal(2, entries.Count);
        var sodium = Assert.Single(entries, m => m.Id == "sodium");
        Assert.True(sodium.Enabled);
        Assert.Equal("Sodium", sodium.Name);
        Assert.Equal("0.5.11", sodium.Version);
        Assert.Contains("fabric-api", sodium.Dependencies);

        var disabled = Assert.Single(entries, m => m.Id == "oldmod");
        Assert.False(disabled.Enabled);
    }

    [Fact]
    public void Toggle_Renames_Mod_File()
    {
        var gameDir = TempDir();
        var mods = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(mods);
        var jar = Path.Combine(mods, "sodium.jar");
        WriteFabricJar(jar, "sodium", "Sodium", "0.5.11");

        Assert.True(ModScanner.SetEnabled(gameDir, "sodium", enabled: false));
        Assert.False(File.Exists(jar));
        Assert.True(File.Exists(jar + ".disabled"));
        Assert.False(ModScanner.Scan(gameDir, LoaderKind.Fabric)[0].Enabled);

        Assert.True(ModScanner.SetEnabled(gameDir, "sodium", enabled: true));
        Assert.True(File.Exists(jar));
        Assert.False(File.Exists(jar + ".disabled"));
    }

    [Fact]
    public void Unknown_Mod_Toggle_Returns_False()
    {
        var gameDir = TempDir();
        Directory.CreateDirectory(Path.Combine(gameDir, "mods"));
        Assert.False(ModScanner.SetEnabled(gameDir, "missing", enabled: false));
    }

    [Fact]
    public void Forge_Mod_Uses_Metadata_Id()
    {
        var gameDir = TempDir();
        var mods = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(mods);
        WriteForgeJar(Path.Combine(mods, "ForgeLib-1.0.jar"), "forge_lib", "Forge Lib", "1.2.3");

        var entry = Assert.Single(ModScanner.Scan(gameDir, LoaderKind.Forge));

        Assert.Equal("forge_lib", entry.Id);
        Assert.Equal("Forge Lib", entry.Name);
        Assert.Equal("1.2.3", entry.Version);
    }

    [Fact]
    public void Toggle_Forge_Mod_Accepts_Metadata_Id_And_File_Stem()
    {
        var gameDir = TempDir();
        var mods = Path.Combine(gameDir, "mods");
        Directory.CreateDirectory(mods);
        var jar = Path.Combine(mods, "ForgeLib-1.0.jar");
        WriteForgeJar(jar, "forge_lib", "Forge Lib", "1.2.3");

        Assert.True(ModScanner.SetEnabled(gameDir, "forge_lib", enabled: false));
        Assert.True(File.Exists(jar + ".disabled"));

        Assert.True(ModScanner.SetEnabled(gameDir, "ForgeLib-1.0", enabled: true));
        Assert.True(File.Exists(jar));
        Assert.False(File.Exists(jar + ".disabled"));
    }

    private static void WriteFabricJar(string path, string id, string name, string version)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("fabric.mod.json");
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write($$"""
            { "id": "{{id}}", "name": "{{name}}", "version": "{{version}}",
              "depends": { "minecraft": "1.21", "fabric-api": "*" } }
            """);
    }

    private static void WriteForgeJar(string path, string modId, string name, string version)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        var entry = archive.CreateEntry("META-INF/mods.toml");
        using var writer = new StreamWriter(entry.Open(), Encoding.UTF8);
        writer.Write($$"""
            modLoader="javafml"
            loaderVersion="[40,)"
            license="MIT"
            [[mods]]
            modId="{{modId}}"
            version="{{version}}"
            displayName="{{name}}"
            """);
    }

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-mods", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public sealed class ExternalImporterTests
{
    [Fact]
    public async Task Detects_MultiMc_And_Copies_Game_Content()
    {
        var source = TempDir();
        Directory.CreateDirectory(Path.Combine(source, "minecraft", "mods"));
        File.WriteAllBytes(Path.Combine(source, "minecraft", "mods", "sodium.jar"), []);
        File.WriteAllText(Path.Combine(source, "minecraft", "options.txt"), "lang:zh_cn");
        Directory.CreateDirectory(Path.Combine(source, "minecraft", "libraries", "x"));
        File.WriteAllBytes(Path.Combine(source, "minecraft", "libraries", "x", "big.jar"), []);
        File.WriteAllText(Path.Combine(source, "mmc-pack.json"), """
            { "formatVersion": 1, "components": [
              { "uid": "net.minecraft", "version": "1.20.1" },
              { "uid": "net.fabricmc.fabric-loader", "version": "0.15.0" } ] }
            """);
        File.WriteAllText(Path.Combine(source, "instance.cfg"), "name=My Pack\n");

        var candidate = ExternalImporter.Detect(source);
        Assert.Equal("multimc", candidate.SourceKind);
        Assert.Equal("1.20.1", candidate.GameVersion);
        Assert.Equal(LoaderKind.Fabric, candidate.Loader);
        Assert.Equal("0.15.0", candidate.LoaderVersion);
        Assert.Equal("My Pack", candidate.Name);
        Assert.Equal(Path.Combine(source, "minecraft"), candidate.GameDir);

        var paths = Paths(TempDir());
        var instanceId = await ExternalImporter.CopyAsync(
            candidate, paths, null, new InlineProgress<ImportProgress>(_ => { }), CancellationToken.None);

        var dest = Path.Combine(paths.InstancesDir, instanceId);
        Assert.True(File.Exists(Path.Combine(dest, "mods", "sodium.jar")));
        Assert.True(File.Exists(Path.Combine(dest, "options.txt")));
        Assert.False(Directory.Exists(Path.Combine(dest, "libraries")));
    }

    [Fact]
    public void Detects_Vanilla_Minecraft_Directory()
    {
        var source = TempDir();
        Directory.CreateDirectory(Path.Combine(source, "versions", "1.21.1"));
        File.WriteAllText(Path.Combine(source, "versions", "1.21.1", "1.21.1.json"), """
            { "id": "1.21.1", "mainClass": "net.minecraft.client.main.Main", "libraries": [] }
            """);

        var candidate = ExternalImporter.Detect(source);

        Assert.Equal("vanilla", candidate.SourceKind);
        Assert.Equal("1.21.1", candidate.GameVersion);
        Assert.Equal(LoaderKind.Vanilla, candidate.Loader);
        Assert.Equal(source, candidate.GameDir);
    }

    [Fact]
    public void Rejects_Unknown_Directory()
    {
        var ex = Assert.Throws<Launcher.Core.Install.InstallException>(() =>
            ExternalImporter.Detect(TempDir()));
        Assert.Equal("E3004", ex.ErrorCode);
    }

    private static LauncherPaths Paths(string root) => new(root, root);

    private static string TempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-import", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}

public sealed class InstanceDeleteTests
{
    [Fact]
    public async Task Delete_With_KeepSaves_Exports_Saves()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-delete", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var paths = new LauncherPaths(root, root);
        paths.EnsureCreated();

        var sqlite = new SqliteStore(paths);
        await sqlite.InitializeAsync();
        var settings = new AppSettingsStore(paths);
        var facade = new LauncherFacade(
            paths, settings, sqlite, NullLogger<LauncherFacade>.Instance);

        var instanceId = "to-delete";
        var gameDir = Path.Combine(paths.InstancesDir, instanceId);
        Directory.CreateDirectory(Path.Combine(gameDir, "saves", "world"));
        await File.WriteAllTextAsync(Path.Combine(gameDir, "saves", "world", "level.dat"), "x");
        await sqlite.UpsertInstanceAsync(
            instanceId, "ToDelete", gameDir, "1.21.1", LoaderKind.Vanilla, null,
            IsolationLevel.ModsConfig, InstanceState.Ready);

        await facade.DeleteInstanceAsync(instanceId, keepSaves: true, CancellationToken.None);

        Assert.False(Directory.Exists(gameDir));
        Assert.Empty(await sqlite.GetInstancesAsync());
        var exported = Directory.GetDirectories(paths.ExportsDir);
        var export = Assert.Single(exported);
        Assert.True(File.Exists(Path.Combine(export, "saves", "world", "level.dat")));
    }

    [Fact]
    public async Task Delete_Does_Not_Touch_External_Directory()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-delete", Guid.NewGuid().ToString("N"));
        var external = Path.Combine(root, "external-instance");
        Directory.CreateDirectory(Path.Combine(external, "mods"));
        File.WriteAllText(Path.Combine(external, "mods", "keep.jar"), "x");

        var paths = new LauncherPaths(Path.Combine(root, "data"), Path.Combine(root, "data"));
        paths.EnsureCreated();
        var sqlite = new SqliteStore(paths);
        await sqlite.InitializeAsync();
        var facade = new LauncherFacade(
            paths, new AppSettingsStore(paths), sqlite, NullLogger<LauncherFacade>.Instance);

        await sqlite.UpsertInstanceAsync(
            "external", "External", external, "1.21.1", LoaderKind.Vanilla, null,
            IsolationLevel.ModsConfig, InstanceState.Ready);

        await facade.DeleteInstanceAsync("external", keepSaves: false, CancellationToken.None);

        Assert.True(File.Exists(Path.Combine(external, "mods", "keep.jar")));
        Assert.Empty(await sqlite.GetInstancesAsync());
    }
}

internal sealed class InlineProgress<T> : IProgress<T>
{
    private readonly Action<T> _action;

    public InlineProgress(Action<T> action)
    {
        _action = action;
    }

    public void Report(T value) => _action(value);
}
