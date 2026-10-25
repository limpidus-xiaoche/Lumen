using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Launcher.App.Mock;
using Launcher.App.Services;
using Launcher.App.ViewModels;
using Launcher.Protocol;
using Serilog;

namespace Launcher.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var useFake = desktop.Args?.Contains("--fake", StringComparer.OrdinalIgnoreCase) == true
                          || Environment.GetEnvironmentVariable("LUMEN_FAKE") == "1";
            ILauncherFacade facade;
            try
            {
                facade = useFake ? new FakeLauncherFacade() : CoreServices.Create().Facade;
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
                DataContext = new MainWindowViewModel(facade),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static Window BuildStartupErrorWindow(Exception ex)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(28),
            Spacing = 14,
        };
        panel.Children.Add(new TextBlock
        {
            Text = "Lumen 启动失败",
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
            Text = "详细日志位于 %LOCALAPPDATA%\\Lumen\\logs；反馈问题时请附上该目录下最新的 launcher-*.log。",
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
            Title = "Lumen 启动失败",
            Width = 640,
            Height = 320,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = panel,
        };
        ((Button)panel.Children[3]).Click += (_, _) => window.Close();
        return window;
    }
}
