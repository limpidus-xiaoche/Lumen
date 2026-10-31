using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Launcher.App.Mock;
using Launcher.App.Services;
using Launcher.App.ViewModels;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Serilog;

namespace Launcher.App;

public partial class App : Application
{
    /// <summary>启动快校验失败时由 Program 写入；非空则不初始化 Core，只显示阻断窗口。</summary>
    internal static Exception? StartupIntegrityError { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            if (StartupIntegrityError is not null)
            {
                desktop.MainWindow = BuildStartupErrorWindow(
                    StartupIntegrityError,
                    "Lumen 完整性校验失败",
                    "程序文件可能已被修改或损坏，启动器已拒绝运行。请重新安装 Lumen，或从发布页重新下载；详细日志位于 %LOCALAPPDATA%\\Lumen\\logs。");
                base.OnFrameworkInitializationCompleted();
                return;
            }

            var useFake = desktop.Args?.Contains("--fake", StringComparer.OrdinalIgnoreCase) == true
                          || Environment.GetEnvironmentVariable("LUMEN_FAKE") == "1";
            ILauncherFacade facade;
            var fallbackPaths = LauncherPaths.CreateDefault(
                LauncherPaths.IsPortableRequested(Environment.GetCommandLineArgs()));
            var logsDir = fallbackPaths.LogsDir;

            try
            {
                if (useFake)
                {
                    facade = new FakeLauncherFacade();
                }
                else
                {
                    var services = CoreServices.Create();
                    facade = services.Facade;
                    logsDir = services.Paths.LogsDir;
                }
            }
            catch (Exception ex)
            {
                // 不允许静默回退 Fake：启动失败必须让用户看见，并能导出诊断。
                Log.Error(ex, "初始化 Core 失败，终止启动（可用 --fake 进入演示模式）");
                desktop.MainWindow = BuildStartupErrorWindow(ex);
                base.OnFrameworkInitializationCompleted();
                return;
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(facade, logsDir),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window BuildStartupErrorWindow(
        Exception ex,
        string title = "Lumen 启动失败",
        string? hint = null)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 14,
        };
        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeight.Bold,
        });
        panel.Children.Add(new TextBlock
        {
            Text = ex.Message,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new TextBlock
        {
            Text = hint ?? "详细日志位于 %LOCALAPPDATA%\\Lumen\\logs；反馈问题时请附上该目录下最新的 launcher-*.log。",
            Opacity = 0.75,
            TextWrapping = TextWrapping.Wrap,
        });
        panel.Children.Add(new Button
        {
            Content = "关闭",
            HorizontalAlignment = HorizontalAlignment.Right,
        });

        var window = new Window
        {
            Title = title,
            Width = 640,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = panel,
        };
        ((Button)panel.Children[3]).Click += (_, _) => window.Close();
        return window;
    }
}
