using Launcher.Core.Storage;
using Launcher.Protocol;
using Microsoft.Win32;

namespace Launcher.Core.Java;

public sealed class JavaLocator
{
    private readonly LauncherPaths _paths;

    public JavaLocator(LauncherPaths paths)
    {
        _paths = paths;
    }

    public Task<IReadOnlyList<JavaRuntime>> DetectAsync(CancellationToken ct = default) =>
        Task.Run(Detect, ct);

    public IReadOnlyList<JavaRuntime> Detect()
    {
        var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        AddEnvironment(candidates);
        AddRegistry(candidates);
        AddCommonDirectories(candidates);
        AddPath(candidates);
        AddBundledRuntimes(candidates);

        var runtimes = new List<JavaRuntime>();
        foreach (var candidate in candidates)
        {
            if (JavaVersionParser.TryRead(candidate, out var runtime) && runtime.MajorVersion > 0)
            {
                runtimes.Add(runtime);
            }
        }

        return runtimes
            .OrderByDescending(r => r.MajorVersion)
            .ThenBy(r => r.Path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static void AddEnvironment(ISet<string> candidates)
    {
        var home = Environment.GetEnvironmentVariable("JAVA_HOME");
        if (!string.IsNullOrWhiteSpace(home))
        {
            AddJavaHome(candidates, home);
        }
    }

    private static void AddRegistry(ISet<string> candidates)
    {
        var subKeys = new[]
        {
            @"SOFTWARE\JavaSoft\JDK",
            @"SOFTWARE\JavaSoft\JRE",
            @"SOFTWARE\JavaSoft\Java Runtime Environment",
            @"SOFTWARE\JavaSoft\Java Development Kit",
        };

        foreach (var hive in new[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser })
        {
            foreach (var view in new[] { RegistryView.Registry64, RegistryView.Registry32 })
            {
                try
                {
                    using var baseKey = RegistryKey.OpenBaseKey(hive, view);
                    foreach (var subKey in subKeys)
                    {
                        using var key = baseKey.OpenSubKey(subKey);
                        if (key is null)
                        {
                            continue;
                        }

                        foreach (var versionName in key.GetSubKeyNames())
                        {
                            using var versionKey = key.OpenSubKey(versionName);
                            if (versionKey?.GetValue("JavaHome") is string home)
                            {
                                AddJavaHome(candidates, home);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    // 注册表视图不可用时跳过，不影响其他探测路径。
                }
            }
        }
    }

    private static void AddCommonDirectories(ISet<string> candidates)
    {
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var localPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs");

        foreach (var root in new[] { programFiles, programFilesX86, localPrograms })
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                continue;
            }

            foreach (var vendor in new[]
                     {
                         "Java", "Eclipse Adoptium", "Microsoft", "Zulu", "BellSoft",
                         "Amazon Corretto", "Semeru", "GraalVM", "RedHat",
                     })
            {
                var vendorDir = Path.Combine(root, vendor);
                if (!Directory.Exists(vendorDir))
                {
                    continue;
                }

                AddJavaHome(candidates, vendorDir);
                foreach (var child in SafeDirectories(vendorDir))
                {
                    AddJavaHome(candidates, child);
                }
            }
        }
    }

    private static void AddPath(ISet<string> candidates)
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        foreach (var directory in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var exe = Path.Combine(directory.Trim(), "java.exe");
            if (File.Exists(exe))
            {
                candidates.Add(Path.GetFullPath(exe));
            }
        }
    }

    private void AddBundledRuntimes(ISet<string> candidates)
    {
        var root = Path.Combine(_paths.SharedDir, "runtimes");
        if (!Directory.Exists(root))
        {
            return;
        }

        foreach (var exe in SafeFiles(root, "java.exe"))
        {
            candidates.Add(Path.GetFullPath(exe));
        }
    }

    private static void AddJavaHome(ISet<string> candidates, string home)
    {
        var exe = Path.Combine(home, "bin", "java.exe");
        if (File.Exists(exe))
        {
            candidates.Add(Path.GetFullPath(exe));
        }
    }

    private static IEnumerable<string> SafeDirectories(string root)
    {
        try
        {
            return Directory.EnumerateDirectories(root);
        }
        catch (Exception)
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeFiles(string root, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(root, pattern, SearchOption.AllDirectories);
        }
        catch (Exception)
        {
            return [];
        }
    }
}
