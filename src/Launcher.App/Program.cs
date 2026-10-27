using Avalonia;
using Launcher.Core.Storage;
using Serilog;
using Velopack;

namespace Launcher.App;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        // A 的最小脚手架：Velopack 安装/更新钩子必须最先执行（B 接管后可按需扩展）。
        VelopackApp.Build().Run();

        using var mutex = new Mutex(initiallyOwned: true, @"Local\Lumen-Launcher", out var createdNew);
        if (!createdNew)
        {
            return;
        }

        var paths = LauncherPaths.CreateDefault(
            LauncherPaths.IsPortableRequested(args), out var portableFellBack);
        paths.EnsureCreated();
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                Path.Combine(paths.LogsDir, "launcher-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7)
            .CreateLogger();
        Log.Information("Lumen 启动：config={ConfigRoot} local={LocalRoot}", paths.ConfigRoot, paths.LocalRoot);
        if (portableFellBack)
        {
            Log.Warning("便携目录不可写，已回退到用户目录：{LocalRoot}", paths.LocalRoot);
        }

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
            .With(new Win32PlatformOptions
            {
                // 世界层依赖 OpenGlControlBase 的 GL 互操作：优先 ANGLE/EGL，其次 WGL，最后软件渲染（会自动降级）。
                RenderingMode =
                [
                    Win32RenderingMode.AngleEgl,
                    Win32RenderingMode.Wgl,
                    Win32RenderingMode.Software,
                ],
                CompositionMode =
                [
                    Win32CompositionMode.WinUIComposition,
                    Win32CompositionMode.RedirectionSurface,
                ],
                DpiAwareness = Win32DpiAwareness.PerMonitorDpiAware,
            })
            .WithInterFont()
            .LogToTrace();
}
