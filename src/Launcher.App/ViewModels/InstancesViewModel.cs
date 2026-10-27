using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>
/// 实例卡片页：展示每个实例的版本/加载器/状态，支持启动、停止、修复、打开目录与版本设置。
/// </summary>
public partial class InstancesViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;
    private readonly Action<object> _openSubPage;
    private readonly Action _goHome;

    public ObservableCollection<InstanceCardViewModel> Instances { get; } = [];

    [ObservableProperty]
    private string statusText = "尚未加载";

    [ObservableProperty]
    private bool isEmpty;

    [ObservableProperty]
    private bool isBusy;

    public InstancesViewModel(ILauncherFacade facade, Action<object> openSubPage, Action goHome)
    {
        _facade = facade;
        _openSubPage = openSubPage;
        _goHome = goHome;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            var sessions = await _facade.GetRunningSessionsAsync(CancellationToken.None);
            var running = sessions.Select(s => s.InstanceId).ToHashSet(StringComparer.Ordinal);

            Instances.Clear();
            foreach (var instance in snapshot.Instances
                         .OrderByDescending(i => i.LastPlayedAt ?? DateTimeOffset.MinValue)
                         .ThenBy(i => i.Name, StringComparer.CurrentCulture))
            {
                var card = new InstanceCardViewModel(instance) { IsRunning = running.Contains(instance.Id) };
                Instances.Add(card);
            }

            IsEmpty = Instances.Count == 0;
            StatusText = IsEmpty
                ? "还没有实例：到下载页安装原版或加载器版本"
                : $"共 {Instances.Count} 个实例，{running.Count} 个运行中";
        }
        catch (Exception ex)
        {
            StatusText = $"读取实例失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task LaunchAsync(InstanceCardViewModel? card)
    {
        if (card is null || !card.CanLaunch)
        {
            return;
        }

        card.ActivityText = "正在启动…";
        StatusText = $"正在启动 {card.Name}…";
        try
        {
            var progress = new Progress<LaunchEvent>(e => UiThread.Post(() => OnLaunchEvent(card, e)));
            var session = await _facade.LaunchAsync(card.Id, progress, CancellationToken.None);
            card.IsRunning = true;
            StatusText = $"{card.Name} 已启动（PID {session.ProcessId}）";
        }
        catch (Exception ex)
        {
            card.ActivityText = $"启动失败：{ex.Message}";
            StatusText = $"启动失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task StopAsync(InstanceCardViewModel? card)
    {
        if (card is null || !card.CanStop)
        {
            return;
        }

        try
        {
            card.ActivityText = "正在请求停止…";
            await _facade.StopGameAsync(card.Id, StopMode.Graceful, CancellationToken.None);
            StatusText = $"已请求停止 {card.Name}";
        }
        catch (Exception ex)
        {
            card.ActivityText = $"停止失败：{ex.Message}";
            StatusText = $"停止失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RepairAsync(InstanceCardViewModel? card)
    {
        if (card is null || !card.CanRepair || IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var progress = new Progress<InstallProgress>(p => UiThread.Post(() => card.ActivityText = p.Message));
            await _facade.RepairInstanceAsync(card.Id, progress, CancellationToken.None);
            StatusText = $"{card.Name} 修复完成";
            IsBusy = false;
            await LoadAsync();
        }
        catch (Exception ex)
        {
            card.ActivityText = $"修复失败：{ex.Message}";
            StatusText = $"修复失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task OpenFolderAsync(InstanceCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        try
        {
            var detail = await _facade.GetInstanceAsync(card.Id, CancellationToken.None);
            if (!Directory.Exists(detail.GameDir))
            {
                StatusText = $"目录不存在：{detail.GameDir}";
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{detail.GameDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText = $"打开目录失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private void OpenVersionSettings(InstanceCardViewModel? card)
    {
        if (card is null)
        {
            return;
        }

        _openSubPage(new VersionSettingsViewModel(_facade, card.Summary, _goHome));
    }

    private void OnLaunchEvent(InstanceCardViewModel card, LaunchEvent e)
    {
        switch (e.Kind)
        {
            case LaunchEventKind.Stdout:
            case LaunchEventKind.Stderr:
                card.ActivityText = e.Line;
                break;

            case LaunchEventKind.Started:
                card.IsRunning = true;
                break;

            case LaunchEventKind.Crashed:
                card.ActivityText = e.CrashHint ?? e.Line;
                break;

            case LaunchEventKind.Exited:
                card.IsRunning = false;
                card.ActivityText = $"已退出（代码 {e.ExitCode}）";
                StatusText = $"{card.Name} 已退出（代码 {e.ExitCode}）";
                break;
        }
    }
}
