using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>启动页（PCL2 风格：大启动按钮 + 当前版本信息 + 实例选择）。</summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    public ObservableCollection<InstanceSummary> Instances { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText))]
    private InstanceSummary? selectedInstance;

    [ObservableProperty]
    private string statusText = "就绪";

    [ObservableProperty]
    private bool isLaunching;

    /// <summary>当前实例的版本信息，如「1.20.1 · Fabric 0.16.0」。</summary>
    public string VersionText => SelectedInstance is null
        ? "未选择实例"
        : $"{SelectedInstance.VersionId} · {LoaderLabel(SelectedInstance.Loader)}" +
          (SelectedInstance.LoaderVersion is null ? string.Empty : $" {SelectedInstance.LoaderVersion}");

    public HomeViewModel(ILauncherFacade facade)
    {
        _facade = facade;
        _ = LoadAsync();
    }

    private static string LoaderLabel(LoaderKind loader) => loader switch
    {
        LoaderKind.Vanilla => "原版",
        LoaderKind.Fabric => "Fabric",
        LoaderKind.Forge => "Forge",
        LoaderKind.NeoForge => "NeoForge",
        _ => loader.ToString(),
    };

    private async Task LoadAsync()
    {
        var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
        Instances.Clear();
        foreach (var instance in snapshot.Instances)
        {
            Instances.Add(instance);
        }

        SelectedInstance = Instances.FirstOrDefault();
    }

    [RelayCommand]
    private async Task LaunchAsync()
    {
        if (SelectedInstance is null)
        {
            StatusText = "没有可启动的实例";
            return;
        }

        IsLaunching = true;
        StatusText = "正在启动游戏…";
        try
        {
            var progress = new Progress<LaunchEvent>(e =>
                Dispatcher.UIThread.Post(() => OnLaunchEvent(e)));

            await _facade.LaunchAsync(SelectedInstance.Id, progress, CancellationToken.None);
        }
        finally
        {
            IsLaunching = false;
        }
    }

    private void OnLaunchEvent(LaunchEvent e)
    {
        StatusText = e.Kind switch
        {
            LaunchEventKind.Started => "游戏进程已启动（Fake）",
            LaunchEventKind.Exited => $"游戏已退出（代码 {e.ExitCode}）",
            _ => StatusText,
        };
    }
}
