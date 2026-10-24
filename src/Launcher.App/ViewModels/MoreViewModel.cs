using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>功能对接状态行（让用户清楚每个模块是否已接通真实核心）。</summary>
public sealed record FeatureStatus(string Name, string Detail);

/// <summary>更多页：关于、免责声明、功能对接状态、相关链接。</summary>
public partial class MoreViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    [ObservableProperty]
    private string appVersion = "读取中…";

    [ObservableProperty]
    private string statusText = string.Empty;

    /// <summary>功能与核心实现的对接情况。</summary>
    public IReadOnlyList<FeatureStatus> Features { get; } =
    [
        new("版本清单", "已对接 Mojang 官方 version_manifest 接口"),
        new("Java 检测", "已对接本机 Java 运行时扫描"),
        new("下载与安装", "已对接官方资源下载与原版安装"),
        new("启动游戏", "已对接参数拼装与进程启动"),
        new("账号登录", "离线可用，正版登录待接入"),
    ];

    public MoreViewModel(ILauncherFacade facade)
    {
        _facade = facade;
        _ = LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            AppVersion = snapshot.AppVersion;
        }
        catch (Exception ex)
        {
            AppVersion = "未知";
            StatusText = $"读取版本信息失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenProjectPage()
    {
        try
        {
            Process.Start(new ProcessStartInfo("https://github.com/limpidus-xiaoche/Lumen")
            {
                UseShellExecute = true,
            });
            StatusText = "已在浏览器打开项目主页";
        }
        catch (Exception ex)
        {
            StatusText = $"打开失败：{ex.Message}";
        }
    }
}
