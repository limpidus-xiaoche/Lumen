using System.Collections.Concurrent;
using Launcher.Core.Storage;
using Launcher.Protocol;

namespace Launcher.Core.Install;

/// <summary>
/// 统一任务队列：注册、进度、取消、重试与重启恢复。
/// 传入 <see cref="SqliteStore"/> 时任务状态会落库，启动器重启后可恢复未完成任务。
/// </summary>
public sealed class TaskRegistry
{
    /// <summary>中断任务的错误码，UI 据此区分「可重试」与「需修复」。</summary>
    public const string InterruptedErrorCode = TaskErrorCodes.Interrupted;

    private const int MaxPersistedTasks = 50;

    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly SqliteStore? _store;
    private readonly SemaphoreSlim _restoreGate = new(1, 1);
    private readonly object _writeLock = new();
    private Task _pendingWrites = Task.CompletedTask;
    private bool _restored;

    public TaskRegistry(SqliteStore? store = null)
    {
        _store = store;
    }

    public string Start(
        TaskKind kind,
        string? instanceId,
        CancellationTokenSource cts,
        Func<CancellationToken, Task>? retry,
        int attempts = 1)
    {
        var id = Guid.NewGuid().ToString("N");
        var info = new TaskInfo(id, instanceId, kind, TaskState.Running, 0, "开始", attempts, null);
        _entries[id] = new Entry(info, cts, retry);
        Persist(info);
        return id;
    }

    public void Report(string taskId, double percent, string message)
    {
        if (_entries.TryGetValue(taskId, out var entry))
        {
            var info = entry.Info with
            {
                // Percent = -1 表示总量未知，界面走不确定进度条；其余钳制到 0–100。
                Percent = percent < 0 ? -1 : Math.Clamp(percent, 0, 100),
                Message = message,
                State = TaskState.Running,
            };
            entry.Info = info;
            Persist(info);
        }
    }

    public void Complete(string taskId, TaskState state, string? errorCode = null, string? message = null)
    {
        if (_entries.TryGetValue(taskId, out var entry))
        {
            var info = entry.Info with
            {
                State = state,
                Percent = state == TaskState.Succeeded ? 100 : entry.Info.Percent,
                Message = message ?? entry.Info.Message,
                ErrorCode = errorCode,
            };
            entry.Info = info;
            Persist(info);
        }
    }

    public bool Cancel(string taskId)
    {
        if (!_entries.TryGetValue(taskId, out var entry))
        {
            return false;
        }

        entry.Cancellation.Cancel();
        Complete(taskId, TaskState.Cancelled, message: "已取消");
        return true;
    }

    public bool Retry(string taskId)
    {
        if (!_entries.TryGetValue(taskId, out var entry) || entry.Retry is null)
        {
            return false;
        }

        var retry = entry.Retry;
        var cts = new CancellationTokenSource();
        var newTaskId = Start(
            entry.Info.Kind,
            entry.Info.InstanceId,
            cts,
            retry,
            entry.Info.Attempts + 1);
        _ = Task.Run(async () =>
        {
            try
            {
                await retry(cts.Token);
                Complete(newTaskId, TaskState.Succeeded);
            }
            catch (OperationCanceledException)
            {
                Complete(newTaskId, TaskState.Cancelled, message: "已取消");
            }
            catch (Exception ex)
            {
                Complete(newTaskId, TaskState.Failed, "E3003", ex.Message);
            }
        }, CancellationToken.None);
        return true;
    }

    public IReadOnlyList<TaskInfo> Snapshot() =>
        _entries.Values.Select(e => e.Info).ToArray();

    public TaskInfo? Get(string taskId) =>
        _entries.TryGetValue(taskId, out var entry) ? entry.Info : null;

    /// <summary>从持久化记录恢复任务；运行中被中断的任务标记为失败并提示可修复。</summary>
    public async Task RestoreAsync(CancellationToken ct = default)
    {
        if (_store is null)
        {
            return;
        }

        await _restoreGate.WaitAsync(ct);
        try
        {
            if (_restored)
            {
                return;
            }

            _restored = true;
            var rows = await _store.GetRecentTasksAsync(MaxPersistedTasks, ct);
            foreach (var row in rows)
            {
                var interrupted = row.State is TaskState.Running or TaskState.Queued or TaskState.Paused;
                var info = interrupted
                    ? row with
                    {
                        State = TaskState.Failed,
                        ErrorCode = InterruptedErrorCode,
                        Message = "上次运行中断（可修复）",
                    }
                    : row;

                if (_entries.TryAdd(row.TaskId, new Entry(info, new CancellationTokenSource(), null)))
                {
                    if (interrupted)
                    {
                        Persist(info);
                    }
                }
            }

            await _store.PruneTasksAsync(MaxPersistedTasks, ct);
        }
        finally
        {
            _restoreGate.Release();
        }
    }

    /// <summary>等待所有排队中的落库写入完成（测试与退出前使用）。</summary>
    public Task FlushAsync()
    {
        lock (_writeLock)
        {
            return _pendingWrites;
        }
    }

    private void Persist(TaskInfo info)
    {
        if (_store is null)
        {
            return;
        }

        var store = _store;
        lock (_writeLock)
        {
            _pendingWrites = _pendingWrites
                .ContinueWith(
                    _ => store.SaveTaskAsync(info),
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default)
                .Unwrap()
                .ContinueWith(_ => { }, TaskScheduler.Default);
        }
    }

    private sealed class Entry
    {
        public Entry(TaskInfo info, CancellationTokenSource cancellation, Func<CancellationToken, Task>? retry)
        {
            Info = info;
            Cancellation = cancellation;
            Retry = retry;
        }

        public TaskInfo Info { get; set; }
        public CancellationTokenSource Cancellation { get; }
        public Func<CancellationToken, Task>? Retry { get; }
    }
}
