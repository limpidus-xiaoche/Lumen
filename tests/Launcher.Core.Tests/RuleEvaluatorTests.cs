using Launcher.Core.Meta;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class RuleEvaluatorTests
{
    private static readonly RuleContext WindowsX64 = new()
    {
        OsName = "windows",
        OsArch = "x86_64",
        OsVersion = "10.0.22631",
        Features = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase)
        {
            ["is_demo_user"] = false,
            ["has_custom_resolution"] = true,
        },
    };

    [Fact]
    public void No_Rules_Allows()
    {
        Assert.True(RuleEvaluator.IsAllowed(null, WindowsX64));
        Assert.True(RuleEvaluator.IsAllowed([], WindowsX64));
    }

    [Fact]
    public void Only_Mismatching_Disallow_Denies()
    {
        var rules = new List<Rule> { new() { Action = "disallow", Os = new OsRule { Name = "osx" } } };
        Assert.False(RuleEvaluator.IsAllowed(rules, WindowsX64));
    }

    [Fact]
    public void Allow_Windows_Denies_Osx()
    {
        var rules = new List<Rule>
        {
            new() { Action = "allow", Os = new OsRule { Name = "windows" } },
            new() { Action = "disallow", Os = new OsRule { Name = "osx" } },
        };

        Assert.True(RuleEvaluator.IsAllowed(rules, WindowsX64));
    }

    [Fact]
    public void Arch_Must_Match()
    {
        var rules = new List<Rule>
        {
            new() { Action = "allow", Os = new OsRule { Arch = "x86" } },
        };

        Assert.False(RuleEvaluator.IsAllowed(rules, WindowsX64));
    }

    [Fact]
    public void Features_Must_Match()
    {
        var rules = new List<Rule>
        {
            new() { Action = "allow", Features = new Dictionary<string, bool> { ["is_demo_user"] = true } },
        };

        Assert.False(RuleEvaluator.IsAllowed(rules, WindowsX64));
    }
}
