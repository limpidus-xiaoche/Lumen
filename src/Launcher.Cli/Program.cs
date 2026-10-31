using System.CommandLine;
using Launcher.Core;
using Launcher.Core.Accounts;
using Launcher.Core.Auth;
using Launcher.Core.Download;
using Launcher.Core.Install;
using Launcher.Core.Java;
using Launcher.Core.Launch;
using Launcher.Core.Meta;
using Launcher.Core.Storage;
using Launcher.Core.Update;
using Launcher.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var launcherPaths = LauncherPaths.CreateDefault(
    LauncherPaths.IsPortableRequested(args), out var portableFellBack);
launcherPaths.EnsureCreated();
if (portableFellBack)
{
    Console.Error.WriteLine(
        $"警告：便携目录不可写，已回退到用户目录（{launcherPaths.LocalRoot}）。");
}
// 移除 --portable，避免 System.CommandLine 将其视为未知选项
var cliArgs = args.Where(a => !string.Equals(a, "--portable", StringComparison.OrdinalIgnoreCase)).ToArray();

// 先应用代理设置，再创建任何可能缓存 HttpClient 的客户端。
var settingsStore = new AppSettingsStore(launcherPaths);
if (!NetworkPolicy.TryApply(settingsStore.Load(), out var proxyError) && proxyError is not null)
{
    Console.Error.WriteLine($"警告：代理设置未生效（{proxyError}），继续使用当前代理。");
}

var manifestClient = new ManifestClient(launcherPaths, sourceMode: () => settingsStore.Load().DownloadSource);
var loaderMetaClient = new LoaderMetaClient(
    launcherPaths, sourceMode: () => settingsStore.Load().DownloadSource);
var resolver = new VersionResolver(manifestClient, launcherPaths, loaderMetaClient);
var javaLocator = new JavaLocator(launcherPaths);

var sqlite = new SqliteStore(launcherPaths);
sqlite.InitializeAsync().GetAwaiter().GetResult();

var downloads = new DownloadEngine(sourceMode: () => settingsStore.Load().DownloadSource);
var javaInstaller = new JavaRuntimeInstaller(
    launcherPaths, downloads, NullLogger<JavaRuntimeInstaller>.Instance);
var loaderCatalog = new LoaderCatalog(sourceMode: () => settingsStore.Load().DownloadSource);
var loaderInstaller = new LoaderInstaller(
    launcherPaths,
    downloads,
    resolver,
    javaLocator,
    settingsStore,
    NullLogger<LoaderInstaller>.Instance,
    javaInstaller: javaInstaller);
var installer = new VanillaInstaller(
    launcherPaths,
    resolver,
    downloads,
    sqlite,
    NullLogger<VanillaInstaller>.Instance,
    loaderInstaller);
var modpackInstaller = new ModpackInstaller(
    launcherPaths,
    downloads,
    installer,
    sqlite,
    NullLogger<ModpackInstaller>.Instance);
var accounts = new AccountStore(launcherPaths);
var microsoft = new MicrosoftAuth();
var authlib = new AuthlibAuth();
var launcher = new LaunchService(
    launcherPaths,
    settingsStore,
    sqlite,
    resolver,
    javaLocator,
    new RunningSessionStore(launcherPaths),
    NullLogger<LaunchService>.Instance,
    accounts,
    microsoft,
    new AuthlibInjector(launcherPaths, downloads),
    javaInstaller);
var facade = new LauncherFacade(
    launcherPaths,
    settingsStore,
    sqlite,
    NullLogger<LauncherFacade>.Instance,
    manifestClient,
    javaLocator,
    installer,
    new TaskRegistry(sqlite),
    launcher,
    loaderMetaClient,
    accounts,
    microsoft,
    authlib,
    loaderCatalog,
    modpackInstaller);

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
    Console.WriteLine($"portable={launcherPaths.IsPortable}");
    if (launcherPaths.PortableFellBack)
    {
        Console.WriteLine("portable_fell_back=true");
    }
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
var installLoaderOption = new Option<string>("--loader")
{
    Description = "加载器：vanilla / fabric / quilt / forge / neoforge（默认 vanilla）",
};
var installLoaderVersionOption = new Option<string>("--loader-version")
{
    Description = "加载器版本号，例如 Fabric 0.16.10",
};

var installCommand = new Command("install", "安装原版或带加载器的实例");
installCommand.Arguments.Add(installVersionArgument);
installCommand.Options.Add(installInstanceOption);
installCommand.Options.Add(installNameOption);
installCommand.Options.Add(skipAssetsOption);
installCommand.Options.Add(installLoaderOption);
installCommand.Options.Add(installLoaderVersionOption);
installCommand.SetAction(result =>
{
    var version = result.GetValue(installVersionArgument);
    if (string.IsNullOrWhiteSpace(version))
    {
        Console.Error.WriteLine("缺少版本 ID");
        return;
    }

    var loaderText = result.GetValue(installLoaderOption) ?? "vanilla";
    if (!Enum.TryParse<LoaderKind>(loaderText, ignoreCase: true, out var loader))
    {
        Console.Error.WriteLine($"未知加载器：{loaderText}");
        return;
    }

    var loaderVersion = result.GetValue(installLoaderVersionOption);
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
    Description = "临时离线用户名（不填则使用当前激活账户）",
};

var launchCommand = new Command("launch", "启动实例");
launchCommand.Arguments.Add(launchInstanceArgument);
launchCommand.Options.Add(launchUsernameOption);
launchCommand.SetAction(result =>
{
    var instanceId = result.GetValue(launchInstanceArgument)
        ?? throw new ArgumentException("缺少实例 ID");
    var username = result.GetValue(launchUsernameOption);

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

var loginModeOption = new Option<string>("--mode")
{
    Description = "登录方式：offline（默认）| microsoft | authlib",
};
var loginUsernameOption = new Option<string>("--username")
{
    Description = "离线用户名 / 外置登录用户名",
};
var loginServerOption = new Option<string>("--server")
{
    Description = "外置登录服务器地址（https）",
};
var loginPasswordOption = new Option<string>("--password")
{
    Description = "外置登录密码（仅用于本次登录，不落盘）",
};

var loginCommand = new Command("login", "登录账户");
loginCommand.Options.Add(loginModeOption);
loginCommand.Options.Add(loginUsernameOption);
loginCommand.Options.Add(loginServerOption);
loginCommand.Options.Add(loginPasswordOption);
loginCommand.SetAction(result =>
{
    var mode = (result.GetValue(loginModeOption) ?? "offline").Trim().ToLowerInvariant();
    var authMode = mode switch
    {
        "offline" => AuthMode.Offline,
        "microsoft" => AuthMode.Microsoft,
        "authlib" => AuthMode.Authlib,
        _ => throw new ArgumentException($"未知的登录方式：{mode}"),
    };

    var request = new LoginRequest(
        authMode,
        result.GetValue(loginUsernameOption),
        result.GetValue(loginServerOption),
        result.GetValue(loginPasswordOption));

    var progress = new SyncProgress<LoginEvent>(e =>
    {
        switch (e.Kind)
        {
            case LoginEventKind.DeviceCode:
                Console.WriteLine(e.Message);
                if (!string.IsNullOrEmpty(e.VerificationUrl))
                {
                    Console.WriteLine($"验证地址：{e.VerificationUrl}");
                }

                if (!string.IsNullOrEmpty(e.UserCode))
                {
                    Console.WriteLine($"验证码：{e.UserCode}");
                }

                break;
            case LoginEventKind.WaitingForUser:
                Console.WriteLine(e.Message);
                break;
            default:
                Console.WriteLine(e.Message);
                break;
        }
    });

    try
    {
        var account = facade.LoginAsync(request, progress, CancellationToken.None)
            .GetAwaiter().GetResult();
        Console.WriteLine($"account id={account.Id} type={account.Type} name={account.Username} uuid={account.Uuid}");
    }
    catch (AuthException ex)
    {
        Console.Error.WriteLine($"{ex.ErrorCode}: {ex.Message}");
    }
});

var loadersGameArgument = new Argument<string>("game")
{
    Description = "游戏版本，例如 1.21.1",
};
var loadersKindOption = new Option<string>("--loader")
{
    Description = "fabric | quilt | forge | neoforge（默认全部）",
};
var loadersLimitOption = new Option<int>("--limit")
{
    Description = "每种加载器最多显示 20 个版本",
};

var loadersCommand = new Command("loaders", "列出加载器版本");
loadersCommand.Arguments.Add(loadersGameArgument);
loadersCommand.Options.Add(loadersKindOption);
loadersCommand.Options.Add(loadersLimitOption);
loadersCommand.SetAction(result =>
{
    var game = result.GetValue(loadersGameArgument)
        ?? throw new ArgumentException("缺少游戏版本");
    var limit = result.GetValue(loadersLimitOption);
    if (limit <= 0)
    {
        limit = 20;
    }

    var kindText = result.GetValue(loadersKindOption);
    var kinds = string.IsNullOrWhiteSpace(kindText)
        ? new[] { LoaderKind.Fabric, LoaderKind.Quilt, LoaderKind.Forge, LoaderKind.NeoForge }
        : kindText.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(ParseLoader)
            .Distinct()
            .ToArray();

    foreach (var kind in kinds)
    {
        try
        {
            var versions = facade.GetLoaderVersionsAsync(kind, game, CancellationToken.None)
                .GetAwaiter().GetResult();
            Console.WriteLine($"# {kind} {game} ({versions.Count})");
            foreach (var version in versions.Take(limit))
            {
                Console.WriteLine($"{version.Version}\t{(version.Stable ? "stable" : string.Empty)}");
            }
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"{kind}: {ex.Message}");
        }
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

var modsInstanceArgument = new Argument<string>("instance")
{
    Description = "实例 ID",
};
var modsToggleArgument = new Argument<string>("mod")
{
    Description = "模组 ID（文件名去掉 .jar）",
};
var modsDisableOption = new Option<bool>("--disable")
{
    Description = "禁用该模组（默认启用）",
};

var modsCommand = new Command("mods", "列出实例模组");
modsCommand.Arguments.Add(modsInstanceArgument);
modsCommand.SetAction(result =>
{
    var instanceId = result.GetValue(modsInstanceArgument)
        ?? throw new ArgumentException("缺少实例 ID");
    var mods = facade.ScanModsAsync(instanceId, CancellationToken.None).GetAwaiter().GetResult();
    if (mods.Count == 0)
    {
        Console.WriteLine("没有模组。");
        return;
    }

    foreach (var mod in mods)
    {
        Console.WriteLine(
            $"{(mod.Enabled ? "on " : "off")}\t{mod.Id}\t{mod.Name}\t{mod.Version}\t{mod.FileName}");
    }
});

var modsToggleCommand = new Command("mods-toggle", "启用/禁用模组");
modsToggleCommand.Arguments.Add(modsInstanceArgument);
modsToggleCommand.Arguments.Add(modsToggleArgument);
modsToggleCommand.Options.Add(modsDisableOption);
modsToggleCommand.SetAction(result =>
{
    var instanceId = result.GetValue(modsInstanceArgument)
        ?? throw new ArgumentException("缺少实例 ID");
    var modId = result.GetValue(modsToggleArgument)
        ?? throw new ArgumentException("缺少模组 ID");
    var enabled = !result.GetValue(modsDisableOption);
    facade.SetModEnabledAsync(instanceId, modId, enabled, CancellationToken.None)
        .GetAwaiter().GetResult();
    Console.WriteLine($"{(enabled ? "enabled" : "disabled")} id={modId}");
});

var importPathArgument = new Argument<string>("path")
{
    Description = "外部实例目录（.minecraft / MultiMC / HMCL）",
};

var importCommand = new Command("import", "导入外部实例（复制）");
importCommand.Arguments.Add(importPathArgument);
importCommand.SetAction(result =>
{
    var path = result.GetValue(importPathArgument)
        ?? throw new ArgumentException("缺少目录路径");
    var progress = new SyncProgress<ImportProgress>(p =>
    {
        if (p.Stage == "detect")
        {
            Console.WriteLine($"detected={p.CurrentPath}");
        }
        else if (p.Percent >= 99 || p.BytesDone == p.TotalBytes)
        {
            Console.WriteLine($"copy {p.Percent:F1}%");
        }
    });

    var imported = facade.ImportExternalAsync(path, progress, CancellationToken.None)
        .GetAwaiter().GetResult();
    foreach (var instance in imported)
    {
        Console.WriteLine(
            $"imported id={instance.Id} version={instance.VersionId} loader={instance.Loader}");
    }
});

var modpackPathArgument = new Argument<string>("file")
{
    Description = "整合包文件（.zip/.mrpack/.mmc/.hmcl）或目录",
};
var modpackNameOption = new Option<string>("--name")
{
    Description = "实例显示名（默认使用整合包名称）",
};
var modpackKindOption = new Option<string>("--kind")
{
    Description = "整合包类型：auto（默认）| modrinth | hmcl | multimc | curseforge",
};

var modpackInstallCommand = new Command("install", "安装整合包");
modpackInstallCommand.Arguments.Add(modpackPathArgument);
modpackInstallCommand.Options.Add(modpackNameOption);
modpackInstallCommand.Options.Add(modpackKindOption);
modpackInstallCommand.SetAction(result =>
{
    var file = result.GetValue(modpackPathArgument)
        ?? throw new ArgumentException("缺少整合包路径");
    var kindText = (result.GetValue(modpackKindOption) ?? "auto").Trim().ToLowerInvariant();
    var kind = kindText switch
    {
        "auto" => ModpackKind.Modrinth, // auto 时由安装器内部自动识别
        "modrinth" => ModpackKind.Modrinth,
        "hmcl" => ModpackKind.Hmcl,
        "multimc" => ModpackKind.MultiMc,
        "curseforge" => ModpackKind.CurseForgeDirect,
        _ => throw new ArgumentException($"未知的整合包类型：{kindText}"),
    };

    var request = new ModpackInstallRequest(
        file,
        result.GetValue(modpackNameOption),
        IsolationLevel.ModsConfig,
        kind);

    var progress = new SyncProgress<InstallProgress>(p =>
        Console.WriteLine($"[{p.Stage}] {p.Percent:F1}% {p.Message}"));

    var summary = facade.InstallModpackAsync(request, progress, CancellationToken.None)
        .GetAwaiter().GetResult();
    Console.WriteLine($"installed id={summary.Id} version={summary.VersionId} loader={summary.Loader} state={summary.State}");
});

var modpackCommand = new Command("modpack", "整合包操作");
modpackCommand.Subcommands.Add(modpackInstallCommand);

var deleteInstanceArgument = new Argument<string>("instance")
{
    Description = "要删除的实例 ID",
};
var keepSavesOption = new Option<bool>("--keep-saves")
{
    Description = "先把 saves/screenshots 导出到 exports 再删除",
};

var deleteCommand = new Command("delete", "删除实例");
deleteCommand.Arguments.Add(deleteInstanceArgument);
deleteCommand.Options.Add(keepSavesOption);
deleteCommand.SetAction(result =>
{
    var instanceId = result.GetValue(deleteInstanceArgument)
        ?? throw new ArgumentException("缺少实例 ID");
    facade.DeleteInstanceAsync(instanceId, result.GetValue(keepSavesOption), CancellationToken.None)
        .GetAwaiter().GetResult();
    Console.WriteLine($"deleted id={instanceId}");
});

var instancesCommand = new Command("instances", "列出实例");
instancesCommand.SetAction(_ =>
{
    var snapshot = facade.GetSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
    if (snapshot.Instances.Count == 0)
    {
        Console.WriteLine("没有实例。");
        return;
    }

    foreach (var instance in snapshot.Instances)
    {
        Console.WriteLine(
            $"{instance.Id}\t{instance.Name}\t{instance.VersionId}\t{instance.Loader}\t{instance.State}");
    }
});

var accountsCommand = new Command("accounts", "列出已保存账户");
accountsCommand.SetAction(_ =>
{
    var snapshot = facade.GetSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
    if (snapshot.Accounts.Count == 0)
    {
        Console.WriteLine("没有账户，请先 login。");
        return;
    }

    foreach (var account in snapshot.Accounts)
    {
        Console.WriteLine(
            $"{account.Id}\t{account.Type}\t{account.Username}\t{account.Uuid}" +
            (account.IsActive ? "\tactive" : string.Empty));
    }
});

var accountKeyArgument = new Argument<string>("account")
{
    Description = "账户 ID 或用户名",
};

var useCommand = new Command("use", "切换激活账户");
useCommand.Arguments.Add(accountKeyArgument);
useCommand.SetAction(result =>
{
    var key = result.GetValue(accountKeyArgument)
        ?? throw new ArgumentException("缺少账户 ID 或用户名");
    var snapshot = facade.GetSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
    var match = FindAccount(snapshot, key);
    if (match is null)
    {
        Console.Error.WriteLine($"账户不存在：{key}");
        return;
    }

    facade.SetActiveAccountAsync(match.Id, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"active={match.Id}");
});

var logoutCommand = new Command("logout", "移除账户");
logoutCommand.Arguments.Add(accountKeyArgument);
logoutCommand.SetAction(result =>
{
    var key = result.GetValue(accountKeyArgument)
        ?? throw new ArgumentException("缺少账户 ID 或用户名");
    var snapshot = facade.GetSnapshotAsync(CancellationToken.None).GetAwaiter().GetResult();
    var match = FindAccount(snapshot, key);
    if (match is null)
    {
        Console.Error.WriteLine($"账户不存在：{key}");
        return;
    }

    facade.LogoutAsync(match.Id, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"removed id={match.Id}");
});

var fullIntegrityOption = new Option<bool>("--full")
{
    Description = "全量逐文件校验（默认快校验）",
};

var integrityCommand = new Command("integrity", "校验自身文件完整性（§12.1）");
integrityCommand.Options.Add(fullIntegrityOption);
integrityCommand.SetAction(result =>
{
    var mode = result.GetValue(fullIntegrityOption)
        ? IntegrityCheckMode.Full
        : IntegrityCheckMode.Quick;
    var report = facade.VerifyIntegrityAsync(mode, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"valid={report.IsValid} mode={report.Mode} checked={report.CheckedFiles}");
    Console.WriteLine(report.Message);
    foreach (var failure in report.FailedFiles)
    {
        Console.WriteLine($"FAILED {failure}");
    }

    if (!report.IsValid)
    {
        Console.Error.WriteLine("E8002 完整性校验失败，拒绝自我替换，请重新安装。");
        return 1;
    }

    return 0;
});

var channelOption = new Option<string>("--channel")
{
    Description = "更新通道（默认 win）",
};
var forceUpdateOption = new Option<bool>("--force")
{
    Description = "允许降级到清单版本",
};

var updateCheckCommand = new Command("check", "检查更新");
updateCheckCommand.Options.Add(channelOption);
updateCheckCommand.Options.Add(forceUpdateOption);
updateCheckCommand.SetAction(result =>
{
    try
    {
        var info = facade.CheckUpdateAsync(
            result.GetValue(channelOption), result.GetValue(forceUpdateOption),
            CancellationToken.None).GetAwaiter().GetResult();
        if (info is null)
        {
            Console.WriteLine("没有可用更新（已是最新版本或已拒绝降级）。");
            return 0;
        }

        Console.WriteLine($"version={info.Version}");
        Console.WriteLine($"size={info.Size}");
        Console.WriteLine($"sha256={info.Sha256}");
        Console.WriteLine($"url={info.Url}");
        if (!string.IsNullOrWhiteSpace(info.Notes))
        {
            Console.WriteLine($"notes={info.Notes}");
        }

        return 0;
    }
    catch (UpdateException ex)
    {
        Console.Error.WriteLine($"E8001 {ex.Message}");
        return 1;
    }
});

var updateApplyCommand = new Command("apply", "下载、校验并应用更新");
updateApplyCommand.Options.Add(channelOption);
updateApplyCommand.Options.Add(forceUpdateOption);
updateApplyCommand.SetAction(result =>
{
    try
    {
        var info = facade.CheckUpdateAsync(
            result.GetValue(channelOption), result.GetValue(forceUpdateOption),
            CancellationToken.None).GetAwaiter().GetResult();
        if (info is null)
        {
            Console.WriteLine("没有可用更新。");
            return 0;
        }

        var progress = new SyncProgress<UpdateProgress>(p =>
            Console.WriteLine($"{p.Percent:F0}% {p.Message}"));
        facade.ApplyUpdateAsync(info, progress, CancellationToken.None).GetAwaiter().GetResult();
        Console.WriteLine($"update applied version={info.Version}");
        return 0;
    }
    catch (IntegrityException ex)
    {
        Console.Error.WriteLine($"E8002 {ex.Message}");
        return 1;
    }
    catch (UpdateException ex)
    {
        Console.Error.WriteLine($"E8001 {ex.Message}");
        return 1;
    }
});

var updateCommand = new Command("update", "自动更新（Velopack）");
updateCommand.Subcommands.Add(updateCheckCommand);
updateCommand.Subcommands.Add(updateApplyCommand);

var diagnosticsInstanceOption = new Option<string>("--instance")
{
    Description = "附加指定实例的配置快照",
};
var diagnosticsCommand = new Command("diagnostics", "导出脱敏诊断包");
diagnosticsCommand.Options.Add(diagnosticsInstanceOption);
diagnosticsCommand.SetAction(result =>
{
    var instanceId = result.GetValue(diagnosticsInstanceOption);
    var path = facade.ExportDiagnosticsAsync(instanceId, CancellationToken.None)
        .GetAwaiter().GetResult();
    Console.WriteLine(path);
});

root.Subcommands.Add(integrityCommand);
root.Subcommands.Add(updateCommand);
root.Subcommands.Add(diagnosticsCommand);
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
root.Subcommands.Add(loadersCommand);
root.Subcommands.Add(loaderVersionsCommand);
root.Subcommands.Add(instancesCommand);
root.Subcommands.Add(modsCommand);
root.Subcommands.Add(modsToggleCommand);
root.Subcommands.Add(importCommand);
root.Subcommands.Add(modpackCommand);
root.Subcommands.Add(deleteCommand);
root.Subcommands.Add(loginCommand);
root.Subcommands.Add(accountsCommand);
root.Subcommands.Add(useCommand);
root.Subcommands.Add(logoutCommand);

return root.Parse(cliArgs).Invoke();

static LoaderKind ParseLoader(string text) => text.Trim().ToLowerInvariant() switch
{
    "fabric" => LoaderKind.Fabric,
    "quilt" => LoaderKind.Quilt,
    "forge" => LoaderKind.Forge,
    "neoforge" => LoaderKind.NeoForge,
    var other => throw new ArgumentException($"未知的加载器：{other}"),
};

static AccountSummary? FindAccount(LauncherSnapshot snapshot, string key) =>
    snapshot.Accounts.FirstOrDefault(a => a.Id == key)
    ?? snapshot.Accounts.FirstOrDefault(a =>
        string.Equals(a.Username, key, StringComparison.OrdinalIgnoreCase));

internal sealed class SyncProgress<T> : IProgress<T>
{
    private readonly Action<T> _report;

    public SyncProgress(Action<T> report)
    {
        _report = report;
    }

    public void Report(T value) => _report(value);
}
