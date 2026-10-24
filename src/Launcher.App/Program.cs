using Avalonia;
using Launcher.Core.Storage;
using Serilog;

namespace Launcher.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        using var mutex = new Mutex(initiallyOwned: true, @"Local\Lumen-Launcher", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        var paths = LauncherPaths.CreateDefault(LauncherPaths.IsPortableRequested(args));
        paths.EnsureCreated();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(paths.LogsDir, "launcher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
        Log.Information("Lumen 启动：config={ConfigRoot} local={LocalRoot}", paths.ConfigRoot, paths.LocalRoot);

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "未处理异常");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "未观察的任务异常");
            e.SetObserved();
        };

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            Log.CloseAndFlush();
        }
    }

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
