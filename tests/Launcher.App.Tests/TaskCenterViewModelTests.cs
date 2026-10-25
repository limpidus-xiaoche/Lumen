using Launcher.App.Mock;
using Launcher.App.ViewModels;
using Launcher.Protocol;
using Xunit;

namespace Launcher.App.Tests;

public sealed class TaskCenterViewModelTests
{
    private static async Task<TaskCenterViewModel> LoadAsync()
    {
        var viewModel = new TaskCenterViewModel(new FakeLauncherFacade());
        await viewModel.RefreshCommand.ExecuteAsync(null);
        return viewModel;
    }

    [Fact]
    public async Task TaskCenter_Loads_Tasks_With_Active_First()
    {
        var viewModel = await LoadAsync();

        Assert.Equal(5, viewModel.Tasks.Count);
        Assert.False(viewModel.IsEmpty);
        Assert.Contains("2 个进行中", viewModel.StatusText);

        // 进行中的任务排在最前，且实例名来自快照而非原始 id。
        Assert.True(viewModel.Tasks[0].IsActive);
        Assert.True(viewModel.Tasks[1].IsActive);
        Assert.Contains(viewModel.Tasks, t => t.InstanceText == "Fabric 1.20.1");
    }

    [Fact]
    public async Task Unknown_Total_Uses_Indeterminate_Progress()
    {
        var viewModel = await LoadAsync();

        var unknown = viewModel.Tasks.Single(t => t.Info.TaskId == "fake-task-unknown");
        Assert.Equal(-1, unknown.Info.Percent);
        Assert.True(unknown.IsIndeterminate);
        Assert.Equal("准备中…", unknown.PercentText);
        Assert.True(unknown.CanCancel);
    }

    [Fact]
    public async Task Cancel_Moves_Task_To_Cancelled()
    {
        var viewModel = await LoadAsync();
        var running = viewModel.Tasks.First(t => t.Info.TaskId == "fake-task-run");

        await running.CancelCommand.ExecuteAsync(null);

        var cancelled = viewModel.Tasks.Single(t => t.Info.TaskId == "fake-task-run");
        Assert.Equal(TaskState.Cancelled, cancelled.Info.State);
        Assert.False(cancelled.CanCancel);
        Assert.True(cancelled.CanRetry);
    }

    [Fact]
    public async Task Retry_Restarts_Failed_Task_With_Next_Attempt()
    {
        var viewModel = await LoadAsync();
        var failed = viewModel.Tasks.First(t => t.Info.TaskId == "fake-task-failed");
        Assert.True(failed.CanRetry);
        Assert.Equal(2, failed.Info.Attempts);

        await failed.RetryCommand.ExecuteAsync(null);

        var retried = viewModel.Tasks.Single(t => t.Info.Attempts == 3);
        Assert.True(retried.IsActive);
        Assert.Equal(0, retried.Info.Percent);
    }

    [Fact]
    public async Task Interrupted_Task_Offers_Repair_Instead_Of_Retry()
    {
        var viewModel = await LoadAsync();

        var interrupted = viewModel.Tasks.Single(t => t.Info.TaskId == "fake-task-interrupted");
        Assert.Equal(TaskErrorCodes.Interrupted, interrupted.Info.ErrorCode);
        Assert.False(interrupted.CanRetry);
        Assert.True(interrupted.CanRepair);
        Assert.True(interrupted.IsFailed);

        var succeeded = viewModel.Tasks.Single(t => t.Info.TaskId == "fake-task-done");
        Assert.False(succeeded.CanCancel);
        Assert.False(succeeded.CanRetry);
        Assert.False(succeeded.CanRepair);
        Assert.Equal("100%", succeeded.PercentText);
    }
}
