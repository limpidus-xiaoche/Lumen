using Launcher.Core;
using Launcher.Core.Accounts;
using Launcher.Core.Auth;
using Launcher.Core.Download;
using Launcher.Core.Install;
using Launcher.Core.Java;
using Launcher.Core.Launch;
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
        var loaderMeta = new LoaderMetaClient(paths);
        var resolver = new VersionResolver(manifest, paths, loaderMeta);
        var javaLocator = new JavaLocator(paths);
        var downloads = new DownloadEngine();
        var loaderCatalog = new LoaderCatalog();
        var loaderInstaller = new LoaderInstaller(
            paths,
            downloads,
            resolver,
            javaLocator,
            settings,
            loggerFactory.CreateLogger<LoaderInstaller>());
        var installer = new VanillaInstaller(
            paths,
            resolver,
            downloads,
            sqlite,
            loggerFactory.CreateLogger<VanillaInstaller>(),
            loaderInstaller);
        var accounts = new AccountStore(paths);
        var microsoft = new MicrosoftAuth();
        var authlib = new AuthlibAuth();
        var authlibInjector = new AuthlibInjector(paths, downloads);
        var launcher = new LaunchService(
            paths,
            settings,
            sqlite,
            resolver,
            javaLocator,
            new RunningSessionStore(paths),
            loggerFactory.CreateLogger<LaunchService>(),
            accounts,
            microsoft,
            authlibInjector);

        var facade = new LauncherFacade(
            paths,
            settings,
            sqlite,
            loggerFactory.CreateLogger<LauncherFacade>(),
            manifestClient: manifest,
            javaLocator: javaLocator,
            installer: installer,
            tasks: new TaskRegistry(sqlite),
            launcher: launcher,
            accounts: accounts,
            microsoft: microsoft,
            authlib: authlib,
            loaderCatalog: loaderCatalog,
            loaderMeta: loaderMeta);

        return new CoreServices(paths, facade);
    }
}
