using Launcher.Core;
using Launcher.Core.Java;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Extensions.Logging;

namespace Launcher.App.Services;

public sealed class CoreServices
{
    public LauncherPaths Paths { get; }
    public ILauncherFacade Facade { get; }

    private CoreServices(LauncherPaths paths, ILauncherFacade facade)
    {
        Paths = paths;
        Facade = facade;
    }

    public static CoreServices Create()
    {
        var args = Environment.GetCommandLineArgs();
        var paths = LauncherPaths.CreateDefault(LauncherPaths.IsPortableRequested(args));
        paths.EnsureCreated();

        var settings = new AppSettingsStore(paths);
        var sqlite = new SqliteStore(paths);
        sqlite.InitializeAsync().GetAwaiter().GetResult();

        var loggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
        var facade = new LauncherFacade(
            paths,
            settings,
            sqlite,
            loggerFactory.CreateLogger<LauncherFacade>(),
            new ManifestClient(paths),
            new JavaLocator(paths));

        return new CoreServices(paths, facade);
    }
}
