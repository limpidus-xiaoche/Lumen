using Launcher.Core;
using NetArchTest.Rules;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class ArchitectureTests
{
    [Fact]
    public void Core_Does_Not_Depend_On_Avalonia_Or_Hosting()
    {
        var result = Types.InAssembly(typeof(LauncherFacade).Assembly)
            .ShouldNot()
            .HaveDependencyOnAny("Avalonia", "Microsoft.Extensions.Hosting", "Microsoft.Extensions.DependencyInjection")
            .GetResult();

        Assert.True(result.IsSuccessful,
            string.Join(Environment.NewLine, result.FailingTypeNames ?? []));
    }
}
