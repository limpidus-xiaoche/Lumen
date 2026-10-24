using Launcher.Core.Install;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class TaskRegistryTests
{
    [Fact]
    public async Task Restore_Marks_Running_Task_As_Interrupted_And_Keeps_History()
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen-tests", Guid.NewGuid().ToString("N"));
        var paths = new LauncherPaths(Path.Combine(root, "config"), Path.Combine(root, "local"));
        paths.EnsureCreated();

        var store = new SqliteStore(paths);
        await store.InitializeAsync();

        var registry = new TaskRegistry(store);
        var finished = registry.Start(TaskKind.Install, "inst-a", new CancellationTokenSource(), retry: null);
        registry.Report(finished, -1, "解析版本清单");
        registry.Complete(finished, TaskState.Succeeded, message: "安装完成");

        var interrupted = registry.Start(TaskKind.Install, "inst-b", new CancellationTokenSource(), retry: null);
        registry.Report(interrupted, 42, "依赖库 · 32/78");
        await registry.FlushAsync();

        // 模拟重启：新的注册表从同一数据库恢复。
        var restored = new TaskRegistry(store);
        await restored.RestoreAsync();

        var done = restored.Get(finished);
        Assert.NotNull(done);
        Assert.Equal(TaskState.Succeeded, done!.State);
        Assert.Equal(100, done.Percent);

        var broken = restored.Get(interrupted);
        Assert.NotNull(broken);
        Assert.Equal(TaskState.Failed, broken!.State);
        Assert.Equal(TaskErrorCodes.Interrupted, broken.ErrorCode);
        Assert.Equal(42, broken.Percent);
        Assert.Contains("中断", broken.Message);

        // 中断任务仍可从任务中心取消，状态会写回数据库。
        Assert.True(restored.Cancel(interrupted));
        Assert.Equal(TaskState.Cancelled, restored.Get(interrupted)!.State);
        await restored.FlushAsync();

        var third = new TaskRegistry(store);
        await third.RestoreAsync();
        Assert.Equal(TaskState.Cancelled, third.Get(interrupted)!.State);

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(root, recursive: true);
    }

    [Fact]
    public async Task Report_Keeps_Unknown_Total_As_MinusOne()
    {
        var registry = new TaskRegistry();
        var id = registry.Start(TaskKind.Install, "inst-c", new CancellationTokenSource(), retry: null);

        registry.Report(id, -1, "准备中");

        Assert.Equal(-1, registry.Get(id)!.Percent);
        await Task.CompletedTask;
    }
}
