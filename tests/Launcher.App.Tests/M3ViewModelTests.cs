using Launcher.App.Mock;
using Launcher.App.Services;
using Launcher.App.ViewModels;
using Launcher.Protocol;
using Xunit;

namespace Launcher.App.Tests;

public sealed class M3ViewModelTests
{
    private static string NewLogsDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "lumen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        for (var i = 0; i < 120; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.True(condition(), message);
    }

    [Fact]
    public async Task Instances_Load_Maps_Cards_With_State_And_Launchability()
    {
        var viewModel = new InstancesViewModel(new FakeLauncherFacade(), _ => { }, () => { });

        await viewModel.LoadAsync();

        Assert.Equal(2, viewModel.Instances.Count);
        Assert.False(viewModel.IsEmpty);
        Assert.Contains("2 个实例", viewModel.StatusText);

        var vanilla = viewModel.Instances.Single(i => i.Id == "demo-vanilla");
        Assert.Equal("1.21.1 · 原版", vanilla.VersionLine);
        Assert.Equal("就绪", vanilla.StatusText);
        Assert.True(vanilla.CanLaunch);
        Assert.False(vanilla.CanStop);
        Assert.False(vanilla.CanRepair);

        var fabric = viewModel.Instances.Single(i => i.Id == "demo-fabric");
        Assert.Equal("1.20.1 · Fabric 0.16.0", fabric.VersionLine);
    }

    [Fact]
    public async Task Instances_Launch_Then_Exit_Updates_Running_State()
    {
        var viewModel = new InstancesViewModel(new FakeLauncherFacade(), _ => { }, () => { });
        await viewModel.LoadAsync();
        var card = viewModel.Instances.Single(i => i.Id == "demo-vanilla");

        await viewModel.LaunchCommand.ExecuteAsync(card);

        Assert.True(card.IsRunning);
        Assert.True(card.CanStop);
        Assert.False(card.CanLaunch);
        Assert.Contains("已启动", viewModel.StatusText);

        await WaitUntilAsync(
            () => !card.IsRunning,
            $"Fake 启动应在 600ms 内退出（IsRunning={card.IsRunning}, Activity={card.ActivityText}, Status={viewModel.StatusText}）");
        Assert.Equal("已退出（代码 0）", card.ActivityText);
        Assert.Contains("已退出", viewModel.StatusText);
    }

    [Fact]
    public async Task Home_Launch_Streams_LiveLog_And_Writes_GameLog()
    {
        var logsDir = NewLogsDir();
        try
        {
            var facade = new FakeLauncherFacade();
            var runLog = new RunLogStore();
            var instances = new InstancesViewModel(facade, _ => { }, () => { });
            var viewModel = new HomeViewModel(
                facade,
                new LogsViewModel(facade, logsDir, runLog),
                runLog,
                instances,
                logsDir,
                _ => { },
                () => { });

            await WaitUntilAsync(() => viewModel.Instances.Count == 2, "实例列表应加载完成");
            Assert.Equal("demo-vanilla", viewModel.SelectedInstance!.Id);

            await viewModel.ToggleLaunchCommand.ExecuteAsync(null);

            Assert.True(runLog.HasContent);
            Assert.NotEmpty(runLog.Lines);
            Assert.Contains(runLog.Lines, l => l.Text.Contains("启动 原版 1.21.1"));
            Assert.NotNull(runLog.FileName);

            await WaitUntilAsync(
                () => !viewModel.IsRunning && runLog.Lines.Any(l => l.Text.Contains("已退出")),
                "Fake 启动的事件流应结束");
            Assert.Contains("游戏已退出", viewModel.StatusText);

            var gameLogs = Directory.GetFiles(Path.Combine(logsDir, "game"), "*.log");
            var text = File.ReadAllText(Assert.Single(gameLogs));
            Assert.Contains("[out] [Fake] 加载世界...", text);
            Assert.Contains("[exit]", text);
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }

    [Fact]
    public async Task Logs_Lists_Game_Log_And_Loads_Preview()
    {
        var logsDir = NewLogsDir();
        try
        {
            var gameDir = Path.Combine(logsDir, "game");
            Directory.CreateDirectory(gameDir);
            var gameLog = Path.Combine(gameDir, "20260101-120000-Test.log");
            await File.WriteAllLinesAsync(gameLog, ["line-one", "line-two", "hello-lumen"]);

            var viewModel = new LogsViewModel(new FakeLauncherFacade(), logsDir, new RunLogStore());
            await viewModel.LoadCommand.ExecuteAsync(null);

            Assert.Equal(2, viewModel.Files.Count);
            var game = viewModel.Files.Single(f => f.IsGame);
            Assert.Equal("游戏", game.KindText);
            Assert.Equal("20260101-120000-Test.log", game.FileName);

            viewModel.SelectedFile = game;

            await WaitUntilAsync(
                () => viewModel.PreviewText.Contains("hello-lumen"),
                "预览应载入日志末尾内容");
            Assert.Contains("游戏", viewModel.PreviewTitle);
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }
}
