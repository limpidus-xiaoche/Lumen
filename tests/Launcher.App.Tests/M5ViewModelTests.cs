using Launcher.App.Mock;
using Launcher.App.ViewModels;
using Launcher.Protocol;
using Xunit;

namespace Launcher.App.Tests;

public sealed class M5ViewModelTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        for (var i = 0; i < 120; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), message);
    }

    [Fact]
    public void Download_Offers_All_Supported_Loaders_For_OneClick()
    {
        var viewModel = new DownloadViewModel(new FakeLauncherFacade());

        Assert.Equal(["Fabric", "Forge", "NeoForge"], viewModel.LoaderKinds);

        viewModel.SelectedLoaderName = "Forge";
        Assert.True(viewModel.CanInstallLoader);
        Assert.Contains("installer", viewModel.LoaderSupportText);

        viewModel.SelectedLoaderName = "NeoForge";
        Assert.True(viewModel.CanInstallLoader);
        Assert.Contains("installer", viewModel.LoaderSupportText);
    }

    [Fact]
    public async Task Download_Installs_Forge_Instance()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = new DownloadViewModel(facade);

        viewModel.SelectedLoaderName = "Forge";
        await WaitUntilAsync(() => viewModel.LoaderVersions.Count > 0, "应获取 Forge 版本列表");
        viewModel.SelectedLoaderGameVersion = "1.21.1";
        await WaitUntilAsync(
            () => viewModel.LoaderVersions.Any(v => v.Version == "47.3.0"),
            "Forge 版本列表应包含 47.3.0");
        viewModel.SelectedLoaderVersion = viewModel.LoaderVersions.First(v => v.Version == "47.3.0");

        await viewModel.InstallLoaderCommand.ExecuteAsync(null);

        Assert.Contains("已安装", viewModel.LoaderStatusText);
        var snapshot = await facade.GetSnapshotAsync(CancellationToken.None);
        Assert.Contains(
            snapshot.Instances,
            i => i.Loader == LoaderKind.Forge && i.VersionId == "1.21.1" && i.LoaderVersion == "47.3.0");
    }

    [Fact]
    public async Task VersionSettings_Switches_Loader_To_Forge()
    {
        var facade = new FakeLauncherFacade();
        var snapshot = await facade.GetSnapshotAsync(CancellationToken.None);
        var instance = snapshot.Instances.First(i => i.Id == "demo-vanilla");
        var reloaded = false;
        var viewModel = new VersionSettingsViewModel(facade, instance, () => { }, () => reloaded = true);

        Assert.True(viewModel.IsVanillaLoader);
        Assert.Contains("原版", viewModel.LoaderCurrentText);

        viewModel.SelectedLoader = viewModel.Loaders.Single(l => l.Value == LoaderKind.Forge);
        await WaitUntilAsync(() => viewModel.LoaderVersions.Count > 0, "应获取 Forge 版本列表");
        Assert.False(viewModel.IsVanillaLoader);

        viewModel.SelectedLoaderVersion = viewModel.LoaderVersions.First(v => v.Version == "47.3.0");
        Assert.True(viewModel.CanSwitchLoader);
        await viewModel.ChangeLoaderCommand.ExecuteAsync(null);

        Assert.Contains("已更换为 Forge", viewModel.LoaderStatusText);
        Assert.True(reloaded);

        var detail = await facade.GetInstanceAsync("demo-vanilla", CancellationToken.None);
        Assert.Equal(LoaderKind.Forge, detail.Summary.Loader);
        Assert.Equal("47.3.0", detail.Summary.LoaderVersion);
        Assert.Contains("Forge", viewModel.VersionText);
        Assert.Contains("Forge", viewModel.LoaderCurrentText);
    }

    [Fact]
    public async Task VersionSettings_Switches_Back_To_Vanilla_And_Clears_Loader_Version()
    {
        var facade = new FakeLauncherFacade();
        var snapshot = await facade.GetSnapshotAsync(CancellationToken.None);
        var instance = snapshot.Instances.First(i => i.Id == "demo-fabric");
        var viewModel = new VersionSettingsViewModel(facade, instance, () => { });

        Assert.False(viewModel.IsVanillaLoader);
        viewModel.SelectedLoader = viewModel.Loaders.Single(l => l.Value == LoaderKind.Vanilla);

        Assert.True(viewModel.IsVanillaLoader);
        Assert.Empty(viewModel.LoaderVersions);
        Assert.True(viewModel.CanSwitchLoader);

        await viewModel.ChangeLoaderCommand.ExecuteAsync(null);

        Assert.Contains("原版", viewModel.LoaderStatusText);
        var detail = await facade.GetInstanceAsync("demo-fabric", CancellationToken.None);
        Assert.Equal(LoaderKind.Vanilla, detail.Summary.Loader);
        Assert.True(string.IsNullOrEmpty(detail.Summary.LoaderVersion));
        Assert.Contains("原版", viewModel.LoaderCurrentText);
    }
}
