using System.Text.Json;
using Launcher.Core.Meta;
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
}
