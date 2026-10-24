using System.CommandLine;
using Launcher.Core.Auth;
using Launcher.Core.Storage;

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

var paths = new Command("paths", "显示数据目录");
paths.SetAction(_ =>
{
    var p = LauncherPaths.CreateDefault();
    Console.WriteLine($"config={p.ConfigRoot}");
    Console.WriteLine($"local={p.LocalRoot}");
});

root.Subcommands.Add(offlineUuid);
root.Subcommands.Add(paths);

return root.Parse(args).Invoke();
