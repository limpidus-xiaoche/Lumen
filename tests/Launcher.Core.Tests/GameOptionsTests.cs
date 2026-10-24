using Launcher.Core.Game;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class GameOptionsTests
{
    [Fact]
    public void Creates_Options_With_Chinese_When_Missing()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-options", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);

        GameOptions.EnsureDefaultLanguage(dir);

        var content = File.ReadAllText(Path.Combine(dir, "options.txt"));
        Assert.Contains("lang:zh_cn", content);
        Directory.Delete(dir, recursive: true);
    }

    [Fact]
    public void Does_Not_Override_Existing_Options()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-options", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "options.txt"), "lang:en_us\n");

        GameOptions.EnsureDefaultLanguage(dir);

        var content = File.ReadAllText(Path.Combine(dir, "options.txt"));
        Assert.Contains("lang:en_us", content);
        Directory.Delete(dir, recursive: true);
    }
}
