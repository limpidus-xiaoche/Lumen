using System.Text.Json;
using Launcher.Core.Launch;
using Launcher.Core.Meta;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class ArgumentBuilderTests
{
    private static readonly Dictionary<string, string> Variables = new(StringComparer.OrdinalIgnoreCase)
    {
        ["auth_player_name"] = "Player",
        ["version_name"] = "1.12.2",
        ["game_directory"] = @"C:\game",
        ["assets_root"] = @"C:\assets",
        ["assets_index_name"] = "1.12",
        ["auth_uuid"] = "00000000000030008000000000000000",
        ["auth_access_token"] = "token",
        ["user_type"] = "msa",
        ["version_type"] = "Lumen",
        ["resolution_width"] = "854",
        ["resolution_height"] = "480",
    };

    [Fact]
    public void Legacy_Arguments_Are_Expanded()
    {
        var version = Detail("""
            {
              "id": "1.12.2",
              "mainClass": "net.minecraft.launchwrapper.Launch",
              "minecraftArguments": "--username ${auth_player_name} --version ${version_name}",
              "libraries": []
            }
            """);

        var command = ArgumentBuilder.Build(
            version, ["lib.jar", "client.jar"], @"C:\game", @"C:\assets", "1.12",
            @"C:\natives", @"C:\libs", Variables, 2048, [], false);

        Assert.Equal("net.minecraft.launchwrapper.Launch", command.MainClass);
        Assert.Contains("--username", command.Args);
        Assert.Contains("Player", command.Args);
        Assert.Contains("--version", command.Args);
        Assert.Contains("1.12.2", command.Args);
        Assert.DoesNotContain(command.Args, a => a.Contains("${", StringComparison.Ordinal));
    }

    [Fact]
    public void Modern_Arguments_Honor_Rules()
    {
        var version = Detail("""
            {
              "id": "1.21.1",
              "mainClass": "net.minecraft.client.main.Main",
              "arguments": {
                "game": [
                  "--username", "${auth_player_name}",
                  { "rules": [ { "action": "allow", "os": { "name": "windows" } } ], "value": "--win" }
                ],
                "jvm": [ "-cp", "${classpath}" ]
              },
              "libraries": []
            }
            """);

        var command = ArgumentBuilder.Build(
            version, ["a.jar"], @"C:\game", @"C:\assets", "17",
            @"C:\natives", @"C:\libs", Variables, 4096, [], true);

        Assert.Contains("--win", command.Args);
        Assert.Contains("-cp", command.Args);
        Assert.Contains("a.jar", command.Args);
        Assert.Contains("-XX:+UseG1GC", command.Args);
    }

    [Fact]
    public void Unresolved_Variable_Throws()
    {
        var version = Detail("""
            {
              "id": "1.21.1",
              "mainClass": "net.minecraft.client.main.Main",
              "arguments": { "game": [ "${not_defined}" ] },
              "libraries": []
            }
            """);

        var ex = Assert.Throws<LaunchException>(() => ArgumentBuilder.Build(
            version, ["a.jar"], @"C:\game", @"C:\assets", "17",
            @"C:\natives", @"C:\libs", Variables, 4096, [], false));

        Assert.Equal("E5002", ex.ErrorCode);
    }

    [Fact]
    public void Classpath_Skips_Natives_Only_And_Disallowed()
    {
        var version = Detail("""
            {
              "id": "1.12.2",
              "mainClass": "net.minecraft.launchwrapper.Launch",
              "libraries": [
                { "name": "com.example:allowed:1.0", "downloads": { "artifact": { "path": "com/example/allowed/1.0/allowed-1.0.jar" } } },
                { "name": "com.example:osx-only:1.0", "rules": [ { "action": "allow", "os": { "name": "osx" } } ] },
                { "name": "com.example:native:1.0", "downloads": { "classifiers": {} } }
              ]
            }
            """);

        var classpath = ArgumentBuilder.BuildClasspath(version, @"C:\libs", @"C:\client.jar");

        Assert.Contains(Path.Combine(@"C:\libs", "com", "example", "allowed", "1.0", "allowed-1.0.jar"), classpath);
        Assert.Contains(@"C:\client.jar", classpath);
        Assert.DoesNotContain(classpath, p => p.Contains("osx-only"));
        Assert.DoesNotContain(classpath, p => p.Contains("native-1.0"));
    }

    private static VersionDetail Detail(string json) =>
        JsonSerializer.Deserialize<VersionDetail>(json) ?? throw new JsonException();
}
