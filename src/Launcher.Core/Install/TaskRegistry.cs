using System.Collections.Concurrent;
using Launcher.Protocol;

namespace Launcher.Core.Install;

public sealed class TaskRegistry
{
    private readonly ConcurrentDictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    public string Start(
        TaskKind kind,
        string? instanceId,
        CancellationTokenSource cts,
        Func<CancellationToken, Task>? retry)
    {
        var id = Guid.NewGuid().ToString("N");
        _entries[id] = new Entry(
            new TaskInfo(id, instanceId, kind, TaskState.Running, 0, "开始", 0, null),
            cts,
            retry);
        return id;
    }

    public void Report(string taskId, double percent, string message)
    {
        if (_entries.TryGetValue(taskId, out var entry))
        {
            entry.Info = entry.Info with
            {
                Percent = Math.Clamp(percent, 0, 100),
                Message = message,
                State = TaskState.Running,
            };
        }
    }

    public void Complete(string taskId, TaskState state, string? errorCode = null, string? message = null)
    {
        if (_entries.TryGetValue(taskId, out var entry))
        {
            entry.Info = entry.Info with
            {
                State = state,
                Percent = state == TaskState.Succeeded ? 100 : entry.Info.Percent,
                Message = message ?? entry.Info.Message,
                ErrorCode = errorCode,
            };
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

        var cts = new CancellationTokenSource();
        var newTaskId = Start(entry.Info.Kind, entry.Info.InstanceId, cts, entry.Retry);
        _ = Task.Run(async () =>
        {
            try
            {
                await entry.Retry(cts.Token);
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
