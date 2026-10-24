using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class HomeViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    [ObservableProperty]
    private string statusText = "M0 骨架已就绪";

    [ObservableProperty]
    private double installProgress;

    [ObservableProperty]
    private bool isBusy;

    public HomeViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
        StatusText = $"版本 {snapshot.AppVersion} · 实例 {snapshot.Instances.Count} 个 · 账户 {snapshot.Accounts.Count} 个";
    }

    [RelayCommand]
    private async Task SimulateInstallAsync()
    {
        IsBusy = true;
        for (var i = 0; i <= 100; i += 5)
        {
            InstallProgress = i;
            StatusText = $"模拟安装 {i}%（FakeLauncherFacade）";
            await Task.Delay(60);
        }

        StatusText = "模拟安装完成";
        IsBusy = false;
    }
}
