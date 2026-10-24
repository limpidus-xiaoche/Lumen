using Launcher.Core.Meta;
using Launcher.Protocol;

namespace Launcher.Core.Launch;

public static class JavaVersionPolicy
{
    public static JavaRuntime Select(
        VersionDetail version,
        IReadOnlyList<JavaRuntime> runtimes,
        string? preferredPath)
    {
        var required = RequiredMajor(version);
        var need64 = required >= 13;

        var candidates = runtimes
            .Where(j => j.MajorVersion >= required && (!need64 || j.Is64Bit))
            .ToList();

        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            var preferred = candidates.FirstOrDefault(j =>
                string.Equals(j.Path, preferredPath, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                return preferred;
            }
        }

        var exact = candidates.FirstOrDefault(j => j.MajorVersion == required);
        if (exact is not null)
        {
            return exact;
        }

        return candidates
            .OrderBy(j => j.MajorVersion)
            .FirstOrDefault()
            ?? throw new LaunchException("E4001", $"未找到满足 Java {required} 的运行环境");
    }

    public static int RequiredMajor(VersionDetail version)
    {
        if (version.JavaVersion?.MajorVersion > 0)
        {
            return version.JavaVersion.MajorVersion;
        }

        var id = version.Id;
        if (id.StartsWith("1.", StringComparison.Ordinal))
        {
            var parts = id.Split('.');
            if (parts.Length >= 2 && int.TryParse(parts[1], out var minor))
            {
                if (minor <= 16) return 8;
                if (minor == 17) return 16;
                if (minor <= 20) return 17;
                return 21;
            }
        }

        if (id.StartsWith("1.20.5", StringComparison.Ordinal) ||
            id.StartsWith("1.21", StringComparison.Ordinal))
        {
            return 21;
        }

        return 21;
    }
}
