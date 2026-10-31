using Launcher.App.Mock;
using Launcher.App.Services;
using Launcher.App.ViewModels;
using Launcher.Protocol;
using Xunit;

namespace Launcher.App.Tests;

public sealed class SettingsMaintenanceTests
{
    private static SettingsViewModel Create(FakeLauncherFacade facade)
    {
        var instances = new InstancesViewModel(facade, _ => { }, () => { });
        var logs = new LogsViewModel(
            facade,
            Path.Combine(Path.GetTempPath(), "lumen-logs-test"),
            new RunLogStore());
        return new SettingsViewModel(facade, instances, logs, _ => { });
    }

    [Fact]
    public async Task QuickIntegrity_ReportsCheckedFiles()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);

        await viewModel.CheckIntegrityCommand.ExecuteAsync("quick");

        Assert.Contains("校验通过", viewModel.MaintenanceStatus);
        Assert.Contains("检查 2 个文件", viewModel.MaintenanceStatus);
        Assert.True(viewModel.IsMaintenanceIdle);
    }

    [Fact]
    public async Task FullIntegrity_UsesFullMode()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);

        await viewModel.CheckIntegrityCommand.ExecuteAsync("full");

        Assert.Contains("校验通过", viewModel.MaintenanceStatus);
        Assert.True(viewModel.IsMaintenanceIdle);
    }

    [Fact]
    public async Task ExportDiagnostics_ShowsArchivePath()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);

        await viewModel.ExportDiagnosticsCommand.ExecuteAsync(null);

        Assert.Contains("diagnostics-fake.zip", viewModel.MaintenanceStatus);
        Assert.True(viewModel.IsMaintenanceIdle);
    }

    [Fact]
    public async Task CheckUpdate_NoUpdateReportsLatestAndDisablesInstall()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);

        await viewModel.CheckUpdateCommand.ExecuteAsync(null);

        Assert.Equal("已是最新版本。", viewModel.MaintenanceStatus);
        Assert.False(viewModel.CanInstallUpdate);
        Assert.True(viewModel.IsMaintenanceIdle);
    }

    [Fact]
    public async Task Save_WritesDownloadSourceAndProxyPatch()
    {
        var facade = new FakeLauncherFacade();
        var viewModel = Create(facade);

        viewModel.SelectedDownloadSource = SettingsViewModel.DownloadSourceMirrorText;
        viewModel.SelectedProxyMode = SettingsViewModel.ProxyModeManualText;
        viewModel.ProxyAddress = "http://127.0.0.1:7890";

        await viewModel.SaveCommand.ExecuteAsync(null);

        Assert.NotNull(facade.LastSettingsPatch);
        Assert.Equal(DownloadSourceMode.Mirror, facade.LastSettingsPatch!.DownloadSource);
        Assert.Equal(ProxyMode.Manual, facade.LastSettingsPatch.ProxyMode);
        Assert.Equal("http://127.0.0.1:7890", facade.LastSettingsPatch.ProxyAddress);
        Assert.Contains("代理", viewModel.StatusText);
    }

    [Fact]
    public void ManualProxy_OnlyEditableInManualMode()
    {
        var viewModel = Create(new FakeLauncherFacade());

        Assert.False(viewModel.IsManualProxy);
        viewModel.SelectedProxyMode = SettingsViewModel.ProxyModeManualText;
        Assert.True(viewModel.IsManualProxy);
        viewModel.SelectedProxyMode = SettingsViewModel.ProxyModeNoneText;
        Assert.False(viewModel.IsManualProxy);
    }
}
