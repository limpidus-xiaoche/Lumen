using Launcher.Core.Java;
using Launcher.Core.Storage;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class JavaVersionParserTests
{
    [Theory]
    [InlineData("1.8.0_432", 8)]
    [InlineData("16.0.2", 16)]
    [InlineData("17.0.19", 17)]
    [InlineData("21", 21)]
    public void ParseMajor_Works(string version, int expected)
    {
        Assert.Equal(expected, JavaVersionParser.ParseMajor(version));
    }

    [Theory]
    [InlineData("x86_64", true)]
    [InlineData("amd64", true)]
    [InlineData("aarch64", true)]
    [InlineData("x86", false)]
    public void Is64BitArch_Works(string arch, bool expected)
    {
        Assert.Equal(expected, JavaVersionParser.Is64BitArch(arch));
    }

    [Fact]
    public void Reads_Release_File_For_32Bit_Java8()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-java", Guid.NewGuid().ToString("N"));
        var home = Path.Combine(root, "jre8");
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        var exe = Path.Combine(home, "bin", "java.exe");
        File.WriteAllText(exe, string.Empty);
        File.WriteAllText(Path.Combine(home, "release"), """
            JAVA_VERSION="1.8.0_432"
            OS_ARCH="x86"
            """);

        Assert.True(JavaVersionParser.TryRead(exe, out var runtime));
        Assert.Equal(8, runtime.MajorVersion);
        Assert.False(runtime.Is64Bit);

        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public void Locator_Finds_Bundled_Runtime()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-java", Guid.NewGuid().ToString("N"));
        var paths = new LauncherPaths(Path.Combine(root, "config"), Path.Combine(root, "local"));
        paths.EnsureCreated();

        var home = Path.Combine(paths.SharedDir, "runtimes", "jdk-21");
        Directory.CreateDirectory(Path.Combine(home, "bin"));
        var exe = Path.Combine(home, "bin", "java.exe");
        File.WriteAllText(exe, string.Empty);
        File.WriteAllText(Path.Combine(home, "release"), """
            JAVA_VERSION="21.0.8"
            OS_ARCH="x86_64"
            """);

        var runtimes = new JavaLocator(paths).Detect();
        Assert.Contains(runtimes, r =>
            r.Path.Equals(exe, StringComparison.OrdinalIgnoreCase) && r.MajorVersion == 21 && r.Is64Bit);

        Directory.Delete(root, recursive: true);
    }
}
