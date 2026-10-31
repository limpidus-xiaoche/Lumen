using Avalonia;
using Launcher.Core.Download;
using Launcher.Core.Storage;
using Launcher.Core.Update;
using Launcher.Protocol;
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

        // 代理设置只影响之后新建的 HttpClient；失败只记日志，不阻断启动。
        try
        {
            if (!NetworkPolicy.TryApply(new AppSettingsStore(paths).Load(), out var proxyError) &&
                proxyError is not null)
            {
                Log.Warning("应用代理设置失败：{Error}", proxyError);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "读取代理设置失败，继续使用系统默认代理");
        }

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            Log.Fatal(e.ExceptionObject as Exception, "未处理异常");
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            Log.Error(e.Exception, "未观察的任务异常");
            e.SetObserved();
        };

        // §12.1：冷启动快校验。Release 缺清单或哈希不符即 fail-closed，不进入主界面。
        try
        {
            var report = new IntegrityService(paths)
                .VerifyAsync(IntegrityCheckMode.Quick)
                .GetAwaiter()
                .GetResult();
            Log.Information(
                "启动完整性快校验：{Message}（检查 {Files} 个文件）",
                report.Message, report.CheckedFiles);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "启动完整性校验失败，拒绝进入主界面");
            App.StartupIntegrityError = ex;
        }

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
