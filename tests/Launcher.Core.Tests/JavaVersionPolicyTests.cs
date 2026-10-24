using Launcher.Core.Launch;
using Launcher.Core.Meta;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class JavaVersionPolicyTests
{
    [Fact]
    public void Selects_Exact_Required_Major()
    {
        var version = Detail("1.21.1", 21);
        var selected = JavaVersionPolicy.Select(version,
            [Java(17, true), Java(21, true)], preferredPath: null);

        Assert.Equal(21, selected.MajorVersion);
    }

    [Fact]
    public void Legacy_Version_Allows_32Bit_Java8()
    {
        var version = Detail("1.12.2", null);
        var selected = JavaVersionPolicy.Select(version,
            [Java(8, false), Java(17, true)], preferredPath: null);

        Assert.Equal(8, selected.MajorVersion);
        Assert.False(selected.Is64Bit);
    }

    [Fact]
    public void Modern_Version_Rejects_32Bit()
    {
        var version = Detail("1.20.1", 17);
        var selected = JavaVersionPolicy.Select(version,
            [Java(17, false), Java(21, true)], preferredPath: null);

        Assert.Equal(21, selected.MajorVersion);
        Assert.True(selected.Is64Bit);
    }

    private static VersionDetail Detail(string id, int? javaMajor) => new()
    {
        Id = id,
        JavaVersion = javaMajor is null ? null : new JavaVersionRef { MajorVersion = javaMajor.Value },
    };

    private static JavaRuntime Java(int major, bool is64) =>
        new($@"C:\Java\{major}", major, is64, "Test", major.ToString());
}
