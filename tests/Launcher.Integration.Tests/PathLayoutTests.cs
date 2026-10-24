using Launcher.Core.Storage;
using Xunit;

namespace Launcher.Integration.Tests;

public sealed class PathLayoutTests
{
    [Fact]
    public void Default_Layout_Splits_Roaming_And_Local()
    {
        var paths = LauncherPaths.CreateDefault();

        Assert.EndsWith("Lumen", paths.ConfigRoot);
        Assert.EndsWith("Lumen", paths.LocalRoot);
        Assert.NotEqual(paths.ConfigRoot, paths.LocalRoot);
        Assert.EndsWith("launcher.json", paths.ConfigFile);
        Assert.EndsWith("launcher.db", paths.DatabasePath);
    }
}
