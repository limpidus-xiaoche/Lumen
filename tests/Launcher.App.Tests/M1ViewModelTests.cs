using Launcher.App.Mock;
using Launcher.App.ViewModels;
using Xunit;

namespace Launcher.App.Tests;

public sealed class M1ViewModelTests
{
    [Fact]
    public async Task DownloadViewModel_Loads_Versions_From_Facade()
    {
        var viewModel = new DownloadViewModel(new FakeLauncherFacade());

        await viewModel.LoadCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.Versions.Count);
        Assert.Equal("1.21.1", viewModel.LatestRelease);
        Assert.Contains("2 个版本", viewModel.StatusText);
    }

    [Fact]
    public async Task SettingsViewModel_Loads_Java_List()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = new SettingsViewModel(
            facade, new InstancesViewModel(facade), new LogsViewModel(facade), _ => { });

        await viewModel.LoadJavasCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Javas);
        Assert.Equal(21, viewModel.Javas[0].MajorVersion);
    }
}
