namespace Launcher.Core.Storage;

public sealed class LauncherPaths
{
    public string ConfigRoot { get; }
    public string LocalRoot { get; }

    public string ConfigFile => Path.Combine(ConfigRoot, "launcher.json");
    public string DatabasePath => Path.Combine(LocalRoot, "launcher.db");
    public string LogsDir => Path.Combine(LocalRoot, "logs");
    public string InstancesDir => Path.Combine(LocalRoot, "instances");
    public string SharedDir => Path.Combine(LocalRoot, "shared");
    public string CacheDir => Path.Combine(LocalRoot, "cache");
    public string ExportsDir => Path.Combine(LocalRoot, "exports");

    public LauncherPaths(string configRoot, string localRoot)
    {
        ConfigRoot = configRoot;
        LocalRoot = localRoot;
    }

    public static LauncherPaths CreateDefault(bool portable = false)
    {
        if (portable)
        {
            var data = Path.Combine(AppContext.BaseDirectory, "data");
            return new LauncherPaths(data, data);
        }

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new LauncherPaths(
            Path.Combine(roaming, "Lumen"),
            Path.Combine(local, "Lumen"));
    }

    public static bool IsPortableRequested(IEnumerable<string> args) =>
        args.Any(a => string.Equals(a, "--portable", StringComparison.OrdinalIgnoreCase));

    public void EnsureCreated()
    {
        Directory.CreateDirectory(ConfigRoot);
        Directory.CreateDirectory(LocalRoot);
        Directory.CreateDirectory(LogsDir);
        Directory.CreateDirectory(InstancesDir);
        Directory.CreateDirectory(SharedDir);
        Directory.CreateDirectory(CacheDir);
        Directory.CreateDirectory(ExportsDir);
    }
}
