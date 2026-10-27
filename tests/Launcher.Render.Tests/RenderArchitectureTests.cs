using Launcher.Render;
using NetArchTest.Rules;
using Xunit;

namespace Launcher.Render.Tests;

public sealed class RenderArchitectureTests
{
    [Fact]
    public void Render_Does_Not_Depend_On_Avalonia()
    {
        var result = Types.InAssembly(typeof(PortalLayout).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Avalonia")
            .GetResult();

        Assert.True(result.IsSuccessful,
            string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }

    [Fact]
    public void Render_Does_Not_Depend_On_Core_Or_Protocol()
    {
        var result = Types.InAssembly(typeof(PortalLayout).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Launcher.Core", "Launcher.Protocol")
            .GetResult();

        Assert.True(result.IsSuccessful,
            string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }
}
