using System.CommandLine;
using Launcher.Core.Auth;
using Launcher.Core.Java;
using Launcher.Core.Meta;
using Launcher.Core.Storage;

var launcherPaths = LauncherPaths.CreateDefault(LauncherPaths.IsPortableRequested(args));
launcherPaths.EnsureCreated();
var manifestClient = new ManifestClient(launcherPaths);
var resolver = new VersionResolver(manifestClient);
var javaLocator = new JavaLocator(launcherPaths);

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

root.Subcommands.Add(offlineUuid);
root.Subcommands.Add(pathsCommand);
root.Subcommands.Add(versionsCommand);
root.Subcommands.Add(javaCommand);
root.Subcommands.Add(resolveCommand);

return root.Parse(args).Invoke();
