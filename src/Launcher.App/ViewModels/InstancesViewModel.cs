using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>
/// 实例卡片页：展示每个实例的版本/加载器/状态，支持启动、停止、修复、打开目录、版本设置，
/// 以及外部实例 / 整合包导入与实例删除。
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
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isBusy;

    /// <summary>导入 / 删除进行中（互斥，避免重复点击）。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIdle))]
    private bool isImporting;

    [ObservableProperty]
    private bool isImportProgressVisible;

    [ObservableProperty]
    private double importPercent;

    [ObservableProperty]
    private bool isImportIndeterminate;

    /// <summary>导入按钮与卡片操作是否可用。</summary>
    public bool IsIdle => !IsImporting && !IsBusy;

    /// <summary>导入代次与状态写入锁：丢弃迟到的进度回调，避免覆盖最终状态文本。</summary>
    private readonly object _importSync = new();
    private int _importGeneration;

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
            var running = await RefreshAsync();
            StatusText = IsEmpty
                ? "还没有实例：到下载页安装原版或加载器版本"
                : $"共 {Instances.Count} 个实例，{running} 个运行中";
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

    /// <summary>
    /// 重新读取实例列表（不覆盖状态文本），供导入 / 删除成功后刷新使用。
    /// </summary>
    /// <returns>运行中的实例数量。</returns>
    private async Task<int> RefreshAsync()
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
        return running.Count;
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

    /// <summary>
    /// 导入外部实例（PCL / 原生 .minecraft / MultiMC / HMCL 目录）。
    /// 供文件选择器与拖拽调用；失败只更新状态文本，不抛出到 UI。
    /// </summary>
    public async Task ImportExternalAsync(string path)
    {
        if (IsImporting || IsBusy || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var generation = BeginImport();
        var message = "导入完成";
        try
        {
            StatusText = $"正在检测实例来源：{Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar))}…";
            var progress = new Progress<ImportProgress>(
                p => UiThread.Post(() => OnImportProgress(p, generation)));
            var imported = await _facade.ImportExternalAsync(path, progress, CancellationToken.None);

            await RefreshAsync();
            var names = string.Join("、", imported.Select(i => i.Name));
            message = imported.Count > 0
                ? $"导入完成：{names}（共 {imported.Count} 个实例）"
                : "导入完成，但没有可用的实例";
        }
        catch (Exception ex)
        {
            message = $"导入失败：{Describe(ex)}";
        }
        finally
        {
            EndImport(generation, message);
        }
    }

    /// <summary>
    /// 导入整合包（.mrpack / .zip），实例名默认取文件名；类型由安装器自动识别。
    /// </summary>
    public async Task ImportModpackAsync(string path)
    {
        if (IsImporting || IsBusy || string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        var generation = BeginImport();
        var name = Path.GetFileNameWithoutExtension(path);
        var message = "导入完成";
        try
        {
            StatusText = $"正在导入整合包：{name}…";
            var request = new ModpackInstallRequest(
                path, name, IsolationLevel.ModsConfig, ModpackKind.Modrinth);
            var progress = new Progress<InstallProgress>(
                p => UiThread.Post(() => OnModpackProgress(p, generation)));
            var instance = await _facade.InstallModpackAsync(request, progress, CancellationToken.None);

            await RefreshAsync();
            message = $"导入完成：{instance.Name}";
        }
        catch (Exception ex)
        {
            message = $"整合包导入失败：{Describe(ex)}";
        }
        finally
        {
            EndImport(generation, message);
        }
    }

    /// <summary>
    /// 删除实例；keepSaves 为 true 时由门面先把存档导出到 exports 再删除目录。
    /// 运行中的实例由门面抛出异常，这里展示提示而不是崩溃。
    /// </summary>
    public async Task DeleteInstanceAsync(InstanceCardViewModel? card, bool keepSaves)
    {
        if (card is null || IsImporting || IsBusy)
        {
            return;
        }

        IsBusy = true;
        var message = $"已删除 {card.Name}";
        try
        {
            await _facade.DeleteInstanceAsync(card.Id, keepSaves, CancellationToken.None);
            if (keepSaves)
            {
                message += "（存档已保留）";
            }
        }
        catch (Exception ex)
        {
            StatusText = $"删除失败：{Describe(ex)}";
            return;
        }
        finally
        {
            IsBusy = false;
        }

        try
        {
            await RefreshAsync();
            StatusText = message;
        }
        catch (Exception ex)
        {
            StatusText = $"{message}，但刷新列表失败：{ex.Message}";
        }
    }

    private int BeginImport()
    {
        lock (_importSync)
        {
            _importGeneration++;
            IsImporting = true;
            IsImportProgressVisible = true;
            IsImportIndeterminate = true;
            ImportPercent = 0;
            return _importGeneration;
        }
    }

    private void EndImport(int generation, string message)
    {
        lock (_importSync)
        {
            if (generation != _importGeneration)
            {
                return;
            }

            _importGeneration++;
            IsImporting = false;
            IsImportProgressVisible = false;
            StatusText = message;
        }
    }

    private void OnImportProgress(ImportProgress p, int generation)
    {
        lock (_importSync)
        {
            if (!IsImporting || generation != _importGeneration)
            {
                return;
            }

            IsImportIndeterminate = p.Percent < 0;
            ImportPercent = p.Percent < 0 ? 0 : p.Percent;
            if (string.Equals(p.Stage, "detect", StringComparison.OrdinalIgnoreCase))
            {
                StatusText = $"正在检测实例来源（{p.CurrentPath ?? "未知"}）…";
                return;
            }

            var percentText = p.Percent >= 0 ? $" {p.Percent:0}%" : string.Empty;
            var fileText = string.IsNullOrWhiteSpace(p.CurrentPath)
                ? string.Empty
                : $" · 当前文件：{Path.GetFileName(p.CurrentPath)}";
            var bytesText = p.TotalBytes > 0
                ? $" · {FormatBytes(p.BytesDone)} / {FormatBytes(p.TotalBytes)}"
                : string.Empty;
            StatusText = p.Stage switch
            {
                "done" => "导入完成：已复制全部实例文件",
                _ => $"正在复制实例文件{percentText}{bytesText}{fileText}",
            };
        }
    }

    private void OnModpackProgress(InstallProgress p, int generation)
    {
        lock (_importSync)
        {
            if (!IsImporting || generation != _importGeneration)
            {
                return;
            }

            IsImportIndeterminate = p.Percent < 0;
            ImportPercent = p.Percent < 0 ? 0 : p.Percent;
            StatusText = p.Percent < 0
                ? p.Message
                : $"{p.Message}（{p.Percent:0}%）";
        }
    }

    private static string Describe(Exception ex) =>
        ex is Launcher.Core.Install.InstallException install
            ? $"{install.ErrorCode}：{install.Message}"
            : ex.Message;

    private static string FormatBytes(long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        < 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024):0.#} MB",
        _ => $"{bytes / (1024.0 * 1024 * 1024):0.#} GB",
    };

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

        _openSubPage(new VersionSettingsViewModel(_facade, card.Summary, _goHome, () => _ = LoadAsync()));
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
