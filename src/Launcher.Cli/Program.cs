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
using Launcher.Protocol;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

var launcherPaths = LauncherPaths.CreateDefault(LauncherPaths.IsPortableRequested(args));
launcherPaths.EnsureCreated();
var manifestClient = new ManifestClient(launcherPaths);
var resolver = new VersionResolver(manifestClient);
var javaLocator = new JavaLocator(launcherPaths);

var settingsStore = new AppSettingsStore(launcherPaths);
var sqlite = new SqliteStore(launcherPaths);
sqlite.InitializeAsync().GetAwaiter().GetResult();
var downloads = new DownloadEngine();
var installer = new VanillaInstaller(
    launcherPaths,
    resolver,
    downloads,
    sqlite,
    NullLogger<VanillaInstaller>.Instance);
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
    new AuthlibInjector(launcherPaths, downloads));
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
    accounts,
    microsoft,
    authlib);

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

var installCommand = new Command("install", "安装原版实例");
installCommand.Arguments.Add(installVersionArgument);
installCommand.Options.Add(installInstanceOption);
installCommand.Options.Add(installNameOption);
installCommand.Options.Add(skipAssetsOption);
installCommand.SetAction(result =>
{
    var version = result.GetValue(installVersionArgument);
    if (string.IsNullOrWhiteSpace(version))
    {
        Console.Error.WriteLine("缺少版本 ID");
        return;
    }

    var instanceId = result.GetValue(installInstanceOption) ?? $"vanilla-{version}";
    var name = result.GetValue(installNameOption) ?? version;
    var request = new InstallRequest(
        instanceId, name, version, LoaderKind.Vanilla, null,
        IsolationLevel.ModsConfig, InstallMode.New, result.GetValue(skipAssetsOption));

    var progress = new Progress<InstallProgress>(p =>
        Console.WriteLine($"[{p.Stage}] {p.Percent:F1}% {p.Message}"));

    var summary = facade.InstallAsync(request, progress, CancellationToken.None).GetAwaiter().GetResult();
    Console.WriteLine($"installed id={summary.Id} state={summary.State}");
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
root.Subcommands.Add(instancesCommand);
root.Subcommands.Add(loginCommand);
root.Subcommands.Add(accountsCommand);
root.Subcommands.Add(useCommand);
root.Subcommands.Add(logoutCommand);

return root.Parse(args).Invoke();

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
