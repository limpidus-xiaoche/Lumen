namespace Launcher.Core.Storage;

public sealed class LauncherPaths
{
    public string ConfigRoot { get; }
    public string LocalRoot { get; }

    /// <summary>是否处于便携模式（数据目录在 exe 同级 data）。</summary>
    public bool IsPortable { get; }

    /// <summary>请求了便携模式但目录不可写，已回退到用户目录（§7）。</summary>
    public bool PortableFellBack { get; }

    public string ConfigFile => Path.Combine(ConfigRoot, "launcher.json");
    public string DatabasePath => Path.Combine(LocalRoot, "launcher.db");
    public string LogsDir => Path.Combine(LocalRoot, "logs");
    public string InstancesDir => Path.Combine(LocalRoot, "instances");
    public string SharedDir => Path.Combine(LocalRoot, "shared");
    public string CacheDir => Path.Combine(LocalRoot, "cache");
    public string ExportsDir => Path.Combine(LocalRoot, "exports");

    public LauncherPaths(
        string configRoot,
        string localRoot,
        bool isPortable = false,
        bool portableFellBack = false)
    {
        ConfigRoot = configRoot;
        LocalRoot = localRoot;
        IsPortable = isPortable;
        PortableFellBack = portableFellBack;
    }

    public static LauncherPaths CreateDefault(bool portable = false) =>
        CreateDefault(portable, out _);

    /// <summary>
    /// 便携模式先做可写探测；Program Files 等只读目录下回退到
    /// <c>%LOCALAPPDATA%\Lumen</c>，并通过 <paramref name="fellBack"/> 提示调用方，不静默失败。
    /// </summary>
    public static LauncherPaths CreateDefault(bool portable, out bool fellBack)
    {
        fellBack = false;
        if (portable)
        {
            var data = Path.Combine(AppContext.BaseDirectory, "data");
            if (CanWrite(data))
            {
                return new LauncherPaths(data, data, isPortable: true);
            }

            fellBack = true;
        }

        var roaming = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new LauncherPaths(
            Path.Combine(roaming, "Lumen"),
            Path.Combine(local, "Lumen"),
            isPortable: false,
            portableFellBack: fellBack);
    }

    public static bool IsPortableRequested(IEnumerable<string> args) =>
        args.Any(a => string.Equals(a, "--portable", StringComparison.OrdinalIgnoreCase));

    /// <summary>目录可写探测：创建目录并写入/删除探针文件。</summary>
    public static bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, $".write-probe-{Guid.NewGuid():N}");
            using (var stream = new FileStream(
                       probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose))
            {
                stream.WriteByte(0);
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

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
