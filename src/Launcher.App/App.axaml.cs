using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Launcher.App.Mock;
using Launcher.App.Services;
using Launcher.App.ViewModels;
using Launcher.Core.Storage;
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
            ILauncherFacade facade;
            var fallbackPaths = LauncherPaths.CreateDefault(
                LauncherPaths.IsPortableRequested(Environment.GetCommandLineArgs()));
            var logsDir = fallbackPaths.LogsDir;

            if (Environment.GetEnvironmentVariable("LUMEN_FAKE") == "1")
            {
                facade = new FakeLauncherFacade();
            }
            else
            {
                try
                {
                    var services = CoreServices.Create();
                    facade = services.Facade;
                    logsDir = services.Paths.LogsDir;
                }
                catch (Exception ex)
                {
                    Log.Error(ex, "初始化 Core 失败，回退到 FakeLauncherFacade");
                    facade = new FakeLauncherFacade();
                }
            }

            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(facade, logsDir),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
