using Launcher.App.Mock;
using Launcher.App.ViewModels;
using Launcher.Protocol;
using Xunit;

namespace Launcher.App.Tests;

public sealed class InstancesMaintenanceTests
{
    private static InstancesViewModel Create(FakeLauncherFacade facade) =>
        new(facade, _ => { }, () => { });

    [Fact]
    public async Task ImportExternal_ReplacesList_AndReportsCompletion()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);
        await viewModel.LoadAsync();

        var source = Path.Combine(Path.GetTempPath(), $"lumen-import-{Guid.NewGuid():N}");
        Directory.CreateDirectory(source);
        try
        {
            await viewModel.ImportExternalAsync(source);

            Assert.Equal(source, facade.LastImportedPath);
            Assert.Contains("导入完成", viewModel.StatusText);
            Assert.Equal(3, viewModel.Instances.Count);
            Assert.False(viewModel.IsImporting);
            Assert.False(viewModel.IsImportProgressVisible);
            Assert.True(viewModel.IsIdle);
        }
        finally
        {
            Directory.Delete(source, recursive: true);
        }
    }

    [Fact]
    public async Task DeleteInstance_WithKeepSaves_RemovesCardAndKeepsFlag()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);
        await viewModel.LoadAsync();
        var card = viewModel.Instances.Single(i => i.Id == "demo-vanilla");

        await viewModel.DeleteInstanceAsync(card, keepSaves: true);

        Assert.Equal("demo-vanilla", facade.LastDeleteInstanceId);
        Assert.True(facade.LastDeleteKeepSaves);
        Assert.DoesNotContain(viewModel.Instances, i => i.Id == "demo-vanilla");
        Assert.Contains("已删除", viewModel.StatusText);
        Assert.Contains("存档已保留", viewModel.StatusText);
    }

    [Fact]
    public async Task DeleteInstance_WithoutKeepSaves_PassesFalse()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);
        await viewModel.LoadAsync();
        var card = viewModel.Instances.Single(i => i.Id == "demo-fabric");

        await viewModel.DeleteInstanceAsync(card, keepSaves: false);

        Assert.Equal("demo-fabric", facade.LastDeleteInstanceId);
        Assert.False(facade.LastDeleteKeepSaves);
        Assert.Single(viewModel.Instances);
        Assert.Contains("已删除", viewModel.StatusText);
    }

    [Fact]
    public async Task ImportModpack_UsesFileNameAndRefreshes()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);
        await viewModel.LoadAsync();

        var packPath = Path.Combine(Path.GetTempPath(), "Better-MC.mrpack");
        await viewModel.ImportModpackAsync(packPath);

        Assert.NotNull(facade.LastModpackRequest);
        Assert.Equal(packPath, facade.LastModpackRequest!.FilePath);
        Assert.Equal("Better-MC", facade.LastModpackRequest.InstanceName);
        Assert.Equal(IsolationLevel.ModsConfig, facade.LastModpackRequest.Isolation);
        Assert.Contains("导入完成", viewModel.StatusText);
        Assert.Equal(3, viewModel.Instances.Count);
        Assert.False(viewModel.IsImporting);
        Assert.True(viewModel.IsIdle);
    }
}
