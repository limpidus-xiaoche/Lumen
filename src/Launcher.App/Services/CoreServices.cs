using Launcher.Core;
using Launcher.Core.Download;
using Launcher.Core.Install;
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
        var manifest = new ManifestClient(paths);
        var resolver = new VersionResolver(manifest);
        var installer = new VanillaInstaller(
            paths,
            resolver,
            new DownloadEngine(),
            sqlite,
            loggerFactory.CreateLogger<VanillaInstaller>());

        var facade = new LauncherFacade(
            paths,
            settings,
            sqlite,
            loggerFactory.CreateLogger<LauncherFacade>(),
            manifest,
            new JavaLocator(paths),
            installer,
            new TaskRegistry());

        return new CoreServices(paths, facade);
    }
}
