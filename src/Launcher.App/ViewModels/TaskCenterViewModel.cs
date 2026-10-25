using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>任务中心一行：把 TaskInfo 转成界面需要的文案与按钮可用性。</summary>
public sealed partial class TaskItemViewModel : ObservableObject
{
    private readonly TaskCenterViewModel _owner;
    private readonly string? _instanceName;

    public TaskItemViewModel(TaskCenterViewModel owner, TaskInfo info, string? instanceName)
    {
        _owner = owner;
        _instanceName = instanceName;
        Info = info;
    }

    public TaskInfo Info { get; }

    public string KindText => Info.Kind switch
    {
        TaskKind.Install => "安装",
        TaskKind.Repair => "修复",
        TaskKind.Modpack => "整合包",
        TaskKind.Launch => "启动",
        TaskKind.Update => "更新",
        TaskKind.Import => "导入",
        _ => Info.Kind.ToString(),
    };

    public string InstanceText => _instanceName
        ?? (string.IsNullOrWhiteSpace(Info.InstanceId) ? "全局任务" : Info.InstanceId!);

    public string StateText => Info.State switch
    {
        TaskState.Queued => "排队中",
        TaskState.Running => "进行中",
        TaskState.Paused => "已暂停",
        TaskState.Succeeded => "已完成",
        TaskState.Failed => "失败",
        TaskState.Cancelled => "已取消",
        _ => Info.State.ToString(),
    };

    public string Message => Info.Message;

    public bool IsActive => Info.State is TaskState.Queued or TaskState.Running or TaskState.Paused;

    public bool IsFailed => Info.State == TaskState.Failed;

    public bool IsCancelled => Info.State == TaskState.Cancelled;

    /// <summary>总量未知（Percent = -1）时进度条走不确定态。</summary>
    public bool IsIndeterminate => Info.Percent < 0 && IsActive;

    public double ProgressValue => Info.Percent < 0 ? 0 : Info.Percent;

    public string PercentText => Info.Percent < 0 ? "准备中…" : $"{Info.Percent:0}%";

    public bool ShowAttempts => Info.Attempts > 1;

    public string AttemptsText => $"第 {Info.Attempts} 次尝试";

    public bool HasError => !string.IsNullOrWhiteSpace(Info.ErrorCode);

    public string ErrorText => Info.ErrorCode ?? string.Empty;

    public bool CanCancel => IsActive;

    /// <summary>中断恢复的任务没有可执行的回调，只能走修复。</summary>
    public bool CanRetry =>
        (Info.State is TaskState.Failed or TaskState.Cancelled)
        && Info.ErrorCode != TaskErrorCodes.Interrupted;

    /// <summary>实例类任务失败/取消后可直接修复（按 SHA1 补缺）。</summary>
    public bool CanRepair =>
        !IsActive
        && Info.State != TaskState.Succeeded
        && !string.IsNullOrWhiteSpace(Info.InstanceId)
        && Info.Kind is TaskKind.Install or TaskKind.Repair or TaskKind.Modpack or TaskKind.Update;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private Task CancelAsync() => _owner.CancelAsync(this);

    [RelayCommand]
    private Task RetryAsync() => _owner.RetryAsync(this);

    [RelayCommand]
    private Task RepairAsync() => _owner.RepairAsync(this);
}

/// <summary>
/// 任务中心：展示 Core 的任务队列，支持取消 / 重试 / 修复；进入页面时恢复上次未完成任务。
/// 只调用 ILauncherFacade，不做任何文件 IO。
/// </summary>
public partial class TaskCenterViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;
    private readonly Dictionary<string, string> _instanceNames = new(StringComparer.Ordinal);
    private DispatcherTimer? _timer;
    private bool _instancesLoaded;

    public ObservableCollection<TaskItemViewModel> Tasks { get; } = [];

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string statusText = "正在加载任务…";

    [ObservableProperty]
    private bool isEmpty;

    public TaskCenterViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    /// <summary>打开任务中心时启动轮询，有进行中任务时每秒刷新一次。</summary>
    public void StartPolling()
    {
        _timer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        if (!_timer.IsEnabled)
        {
            _timer.Start();
        }
    }

    public void StopPolling() => _timer?.Stop();

    private void OnTick(object? sender, EventArgs e)
    {
        if (!Tasks.Any(t => t.IsActive) && !IsBusy)
        {
            return;
        }

        _ = RefreshAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await EnsureInstanceNamesAsync();
            var tasks = await _facade.GetTasksAsync(CancellationToken.None);

            Tasks.Clear();
            foreach (var info in tasks
                         .OrderBy(t => StateRank(t.State))
                         .ThenByDescending(t => t.Attempts))
            {
                Tasks.Add(new TaskItemViewModel(
                    this,
                    info,
                    info.InstanceId is not null && _instanceNames.TryGetValue(info.InstanceId, out var name)
                        ? name
                        : null));
            }

            IsEmpty = Tasks.Count == 0;
            var active = Tasks.Count(t => t.IsActive);
            StatusText = Tasks.Count == 0
                ? "暂无任务：在下载页安装游戏后，进度会出现在这里"
                : active > 0
                    ? $"共 {Tasks.Count} 个任务，{active} 个进行中"
                    : $"共 {Tasks.Count} 个任务";
        }
        catch (Exception ex)
        {
            StatusText = $"读取任务失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    internal async Task CancelAsync(TaskItemViewModel item)
    {
        if (item.IsBusy)
        {
            return;
        }

        item.IsBusy = true;
        try
        {
            await _facade.CancelTaskAsync(item.Info.TaskId, CancellationToken.None);
            StatusText = $"已取消：{item.KindText} · {item.InstanceText}";
        }
        catch (Exception ex)
        {
            StatusText = $"取消失败：{ex.Message}";
        }
        finally
        {
            item.IsBusy = false;
            await RefreshAsync();
        }
    }

    internal async Task RetryAsync(TaskItemViewModel item)
    {
        if (item.IsBusy)
        {
            return;
        }

        item.IsBusy = true;
        try
        {
            await _facade.RetryTaskAsync(item.Info.TaskId, CancellationToken.None);
            StatusText = $"已重新开始：{item.KindText} · {item.InstanceText}";
        }
        catch (Exception ex)
        {
            StatusText = $"重试失败：{ex.Message}";
        }
        finally
        {
            item.IsBusy = false;
            await RefreshAsync();
        }
    }

    /// <summary>修复：按 SHA1 补缺，Core 会登记一条新的修复任务。</summary>
    internal async Task RepairAsync(TaskItemViewModel item)
    {
        if (item.IsBusy || string.IsNullOrWhiteSpace(item.Info.InstanceId))
        {
            return;
        }

        item.IsBusy = true;
        StatusText = $"正在修复：{item.InstanceText}…";
        try
        {
            var progress = new Progress<InstallProgress>(p =>
                Dispatcher.UIThread.Post(() => StatusText = $"正在修复 {item.InstanceText}：{p.Message}"));
            await _facade.RepairInstanceAsync(item.Info.InstanceId!, progress, CancellationToken.None);
            StatusText = $"修复完成：{item.InstanceText}";
        }
        catch (Exception ex)
        {
            StatusText = $"修复失败：{ex.Message}";
        }
        finally
        {
            item.IsBusy = false;
            await RefreshAsync();
        }
    }

    private async Task EnsureInstanceNamesAsync()
    {
        if (_instancesLoaded)
        {
            return;
        }

        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            foreach (var instance in snapshot.Instances)
            {
                _instanceNames[instance.Id] = instance.Name;
            }

            _instancesLoaded = true;
        }
        catch
        {
            // 实例名读取失败时回退显示实例 id。
        }
    }

    private static int StateRank(TaskState state) => state switch
    {
        TaskState.Running => 0,
        TaskState.Queued => 1,
        TaskState.Paused => 2,
        TaskState.Failed => 3,
        TaskState.Cancelled => 4,
        _ => 5,
    };
}
