using System.CommandLine;
using Launcher.Core;
using Launcher.Core.Auth;
using Launcher.Core.Download;
using Launcher.Core.Install;
using Launcher.Core.Java;
using Launcher.Core.Launch;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var launcherPaths = LauncherPaths.CreateDefault(LauncherPaths.IsPortableRequested(args));
launcherPaths.EnsureCreated();
var manifestClient = new ManifestClient(launcherPaths);
var loaderMetaClient = new LoaderMetaClient(launcherPaths);
var resolver = new VersionResolver(manifestClient, loaderMetaClient);
var javaLocator = new JavaLocator(launcherPaths);

var settingsStore = new AppSettingsStore(launcherPaths);
var sqlite = new SqliteStore(launcherPaths);
sqlite.InitializeAsync().GetAwaiter().GetResult();
var installer = new VanillaInstaller(
    launcherPaths,
    resolver,
    new DownloadEngine(),
    sqlite,
    NullLogger<VanillaInstaller>.Instance);
var launcher = new LaunchService(
    launcherPaths,
    settingsStore,
    sqlite,
    resolver,
    javaLocator,
    new RunningSessionStore(launcherPaths),
    NullLogger<LaunchService>.Instance);
var facade = new LauncherFacade(
    launcherPaths,
    settingsStore,
    sqlite,
    NullLogger<LauncherFacade>.Instance,
    manifestClient,
    javaLocator,
    installer,
    new TaskRegistry(),
    launcher,
    loaderMetaClient);

var root = new RootCommand("Lumen CLI");

var nameOption = new Option<string>("--name")
{
    Description = "离线玩家名",
};

var offlineUuid = new Command("offline-uuid", "生成离线 UUID");
offlineUuid.Options.Add(nameOption);
offlineUuid.SetAction(result =>
{
    var name = result.GetValue(nameOption) ?? "Player";
    Console.WriteLine(OfflineAuth.CreateOfflineUuid(name));
});

var pathsCommand = new Command("paths", "显示数据目录");
pathsCommand.SetAction(_ =>
{
    Console.WriteLine($"config={launcherPaths.ConfigRoot}");
    Console.WriteLine($"local={launcherPaths.LocalRoot}");
});

var limitOption = new Option<int>("--limit")
{
    Description = "最多显示的版本数（默认 20）",
};

var versionsCommand = new Command("versions", "列出 Mojang 版本清单");
versionsCommand.Options.Add(limitOption);
versionsCommand.SetAction(result =>
{
    var limit = result.GetValue(limitOption);
    if (limit <= 0)
    {
        limit = 20;
    }

    var manifest = manifestClient.GetManifestAsync(force: false).GetAwaiter().GetResult();
    Console.WriteLine($"latest_release={manifest.LatestRelease}");
    Console.WriteLine($"latest_snapshot={manifest.Latest?.Snapshot}");
    foreach (var version in manifest.Versions.Take(limit))
    {
        Console.WriteLine($"{version.Id}\t{version.Type}");
    }
});

var javaCommand = new Command("java", "列出本机 Java");
javaCommand.SetAction(_ =>
{
    var runtimes = javaLocator.Detect();
    if (runtimes.Count == 0)
    {
        Console.WriteLine("未找到 Java。");
        return;
    }

    foreach (var java in runtimes)
    {
        Console.WriteLine($"{java.MajorVersion}\t{(java.Is64Bit ? "x64" : "x86")}\t{java.Vendor}\t{java.Path}");
    }
});

var idArgument = new Argument<string>("id")
{
    Description = "版本 ID，例如 1.21.1 或 1.20.1-forge-47.2.0",
};

var resolveCommand = new Command("resolve", "解析并合并版本继承");
resolveCommand.Arguments.Add(idArgument);
resolveCommand.SetAction(result =>
{
    var id = result.GetValue(idArgument) ?? throw new ArgumentException("缺少版本 ID");
    var detail = resolver.ResolveAsync(id).GetAwaiter().GetResult();

    Console.WriteLine($"id={detail.Id}");
    Console.WriteLine($"mainClass={detail.MainClass}");
    Console.WriteLine($"libraries={detail.Libraries.Count}");
    Console.WriteLine($"gameArgs={detail.Arguments?.Game.Count ?? 0}");
    Console.WriteLine($"jvmArgs={detail.Arguments?.Jvm.Count ?? 0}");
    Console.WriteLine($"assetIndex={detail.AssetIndex?.Id}");
    Console.WriteLine($"javaMajor={detail.JavaVersion?.MajorVersion}");
});

var installVersionArgument = new Argument<string>("version")
{
    Description = "要安装的版本 ID，例如 1.21.1",
};
var installInstanceOption = new Option<string>("--instance")
{
    Description = "实例 ID（默认 vanilla-<version>）",
};
var installNameOption = new Option<string>("--name")
{
    Description = "实例显示名（默认使用版本 ID）",
};
var skipAssetsOption = new Option<bool>("--skip-assets")
{
    Description = "跳过资源文件下载（仅用于快速验证）",
};
var loaderOption = new Option<string>("--loader")
{
    Description = "加载器：vanilla / fabric / quilt / forge / neoforge（默认 vanilla）",
};
var loaderVersionOption = new Option<string>("--loader-version")
{
    Description = "加载器版本号，例如 Fabric 0.16.10",
};

var installCommand = new Command("install", "安装原版或带加载器的实例");
installCommand.Arguments.Add(installVersionArgument);
installCommand.Options.Add(installInstanceOption);
installCommand.Options.Add(installNameOption);
installCommand.Options.Add(skipAssetsOption);
installCommand.Options.Add(loaderOption);
installCommand.Options.Add(loaderVersionOption);
installCommand.SetAction(result =>
{
    var version = result.GetValue(installVersionArgument);
    if (string.IsNullOrWhiteSpace(version))
    {
        Console.Error.WriteLine("缺少版本 ID");
        return;
    }

    var loaderText = result.GetValue(loaderOption) ?? "vanilla";
    if (!Enum.TryParse<LoaderKind>(loaderText, ignoreCase: true, out var loader))
    {
        Console.Error.WriteLine($"未知加载器：{loaderText}");
        return;
    }

    var loaderVersion = result.GetValue(loaderVersionOption);
    if (loader != LoaderKind.Vanilla && string.IsNullOrWhiteSpace(loaderVersion))
    {
        Console.Error.WriteLine("未指定 --loader-version");
        return;
    }

    var instanceId = result.GetValue(installInstanceOption)
        ?? (loader == LoaderKind.Vanilla
            ? $"vanilla-{version}"
            : $"{version}-{loader.ToString().ToLowerInvariant()}-{loaderVersion}");
    var name = result.GetValue(installNameOption) ?? version;
    var request = new InstallRequest(
        instanceId, name, version, loader, loaderVersion,
        IsolationLevel.ModsConfig, InstallMode.New, result.GetValue(skipAssetsOption));

    var progress = new Progress<InstallProgress>(p =>
        Console.WriteLine($"[{p.Stage}] {p.Percent:F1}% {p.Message}"));

    var summary = facade.InstallAsync(request, progress, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"installed id={summary.Id} state={summary.State} loader={summary.Loader} {summary.LoaderVersion}");
});

var repairInstanceArgument = new Argument<string>("instance")
{
    Description = "要修复的实例 ID",
};

var repairCommand = new Command("repair", "修复实例（按 SHA1 重下缺失文件）");
repairCommand.Arguments.Add(repairInstanceArgument);
repairCommand.SetAction(result =>
{
    var instanceId = result.GetValue(repairInstanceArgument)
        ?? throw new ArgumentException("缺少实例 ID");
    var progress = new Progress<InstallProgress>(p =>
        Console.WriteLine($"[{p.Stage}] {p.Percent:F1}% {p.Message}"));
    facade.RepairInstanceAsync(instanceId, progress, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"repaired id={instanceId}");
});

var launchInstanceArgument = new Argument<string>("instance")
{
    Description = "要启动的实例 ID",
};
var launchUsernameOption = new Option<string>("--username")
{
    Description = "离线用户名（默认 Player）",
};

var launchCommand = new Command("launch", "启动实例（离线账号）");
launchCommand.Arguments.Add(launchInstanceArgument);
launchCommand.Options.Add(launchUsernameOption);
launchCommand.SetAction(result =>
{
    var instanceId = result.GetValue(launchInstanceArgument)
        ?? throw new ArgumentException("缺少实例 ID");
    var username = result.GetValue(launchUsernameOption) ?? "Player";

    var events = new Progress<LaunchEvent>(e =>
    {
        if (e.Kind is LaunchEventKind.Stdout or LaunchEventKind.Stderr)
        {
            Console.WriteLine(e.Line);
        }
        else if (e.Kind == LaunchEventKind.Exited)
        {
            Console.WriteLine(e.CrashHint ?? e.Line);
        }
    });

    var session = launcher.LaunchAsync(instanceId, username, events, CancellationToken.None)
        .GetAwaiter().GetResult();
    Console.WriteLine($"pid={session.ProcessId}");
    var code = session.ExitTask.GetAwaiter().GetResult();
    Console.WriteLine($"exit={code}");
});

var stopInstanceArgument = new Argument<string>("instance")
{
    Description = "要停止的实例 ID",
};

var stopCommand = new Command("stop", "停止实例");
stopCommand.Arguments.Add(stopInstanceArgument);
stopCommand.SetAction(result =>
{
    var instanceId = result.GetValue(stopInstanceArgument)
        ?? throw new ArgumentException("缺少实例 ID");
    launcher.StopGameAsync(instanceId, StopMode.Graceful, CancellationToken.None)
        .GetAwaiter().GetResult();
    Console.WriteLine($"stopped id={instanceId}");
});

var sessionsCommand = new Command("sessions", "列出运行中的游戏");
sessionsCommand.SetAction(_ =>
{
    var sessions = launcher.GetRunningSessionsAsync(CancellationToken.None).GetAwaiter().GetResult();
    if (sessions.Count == 0)
    {
        Console.WriteLine("没有运行中的游戏。");
        return;
    }

    foreach (var session in sessions)
    {
        Console.WriteLine($"{session.InstanceId}\tpid={session.ProcessId}\t{session.VersionId}");
    }
});

var loaderGameArgument = new Argument<string>("game")
{
    Description = "游戏版本，例如 1.21.1",
};
var loaderListKindOption = new Option<string>("--loader")
{
    Description = "fabric / quilt / forge / neoforge（默认全部列出）",
};
var loaderLimitOption = new Option<int>("--limit")
{
    Description = "每个加载器最多列出的版本数（默认 10）",
};

var loaderVersionsCommand = new Command("loader-versions", "列出可下载的加载器版本号");
loaderVersionsCommand.Arguments.Add(loaderGameArgument);
loaderVersionsCommand.Options.Add(loaderListKindOption);
loaderVersionsCommand.Options.Add(loaderLimitOption);
loaderVersionsCommand.SetAction(result =>
{
    var game = result.GetValue(loaderGameArgument) ?? throw new ArgumentException("缺少游戏版本");
    var limit = result.GetValue(loaderLimitOption);
    if (limit <= 0)
    {
        limit = 10;
    }

    var loaderText = result.GetValue(loaderListKindOption);
    var kinds = new List<LoaderKind>();
    if (!string.IsNullOrWhiteSpace(loaderText))
    {
        if (!Enum.TryParse<LoaderKind>(loaderText, ignoreCase: true, out var kind))
        {
            Console.Error.WriteLine($"未知加载器：{loaderText}");
            return;
        }

        kinds.Add(kind);
    }
    else
    {
        kinds.AddRange([LoaderKind.Fabric, LoaderKind.Quilt, LoaderKind.Forge, LoaderKind.NeoForge]);
    }

    foreach (var kind in kinds)
    {
        var versions = loaderMetaClient.GetVersionsAsync(kind, game, CancellationToken.None)
            .GetAwaiter().GetResult();
        Console.WriteLine($"{kind}\t{versions.Count} 个版本\t安装支持={(LoaderMetaClient.SupportsInstall(kind) ? "是" : "否")}");
        foreach (var version in versions.Take(limit))
        {
            Console.WriteLine($"  {version.Version}\t{(version.Stable ? "稳定" : "测试")}");
        }
    }
});

root.Subcommands.Add(offlineUuid);
root.Subcommands.Add(pathsCommand);
root.Subcommands.Add(versionsCommand);
root.Subcommands.Add(javaCommand);
root.Subcommands.Add(resolveCommand);
root.Subcommands.Add(installCommand);
root.Subcommands.Add(repairCommand);
root.Subcommands.Add(launchCommand);
root.Subcommands.Add(stopCommand);
root.Subcommands.Add(sessionsCommand);
root.Subcommands.Add(loaderVersionsCommand);

return root.Parse(args).Invoke();
