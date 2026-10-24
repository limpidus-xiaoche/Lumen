using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using Launcher.Protocol;

namespace Launcher.Core.Java;

public static class JavaVersionParser
{
    public static bool TryRead(string javaExe, out JavaRuntime runtime)
    {
        runtime = null!;
        if (!File.Exists(javaExe))
        {
            return false;
        }

        var home = Path.GetDirectoryName(Path.GetDirectoryName(javaExe));
        if (!string.IsNullOrEmpty(home))
        {
            var release = Path.Combine(home, "release");
            if (File.Exists(release) && TryParseRelease(release, javaExe, out runtime))
            {
                return true;
            }
        }

        return TryRunProperties(javaExe, out runtime);
    }

    public static bool TryParseRelease(string releaseFile, string javaExe, out JavaRuntime runtime)
    {
        runtime = null!;
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(releaseFile))
        {
            var index = line.IndexOf('=');
            if (index <= 0)
            {
                continue;
            }

            var key = line[..index].Trim();
            var value = line[(index + 1)..].Trim().Trim('"');
            values[key] = value;
        }

        if (!values.TryGetValue("JAVA_VERSION", out var versionText))
        {
            return false;
        }

        var arch = values.GetValueOrDefault("OS_ARCH", string.Empty);
        runtime = new JavaRuntime(
            javaExe,
            ParseMajor(versionText),
            Is64BitArch(arch),
            GuessVendor(javaExe),
            versionText);
        return true;
    }

    public static int ParseMajor(string version)
    {
        if (version.StartsWith("1.", StringComparison.Ordinal))
        {
            var rest = version[2..];
            var dot = rest.IndexOf('.');
            var part = dot >= 0 ? rest[..dot] : rest;
            return int.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var legacy)
                ? legacy
                : 0;
        }

        var main = version.Split('.', '-', '_', '+')[0];
        return int.TryParse(main, out var major) ? major : 0;
    }

    public static bool Is64BitArch(string arch) =>
        arch.Contains("64", StringComparison.OrdinalIgnoreCase) ||
        arch.Contains("amd64", StringComparison.OrdinalIgnoreCase) ||
        arch.Contains("aarch64", StringComparison.OrdinalIgnoreCase);

    public static string GuessVendor(string javaExe)
    {
        var path = javaExe.ToLowerInvariant();
        if (path.Contains("temurin") || path.Contains("adoptium")) return "Temurin";
        if (path.Contains("microsoft")) return "Microsoft";
        if (path.Contains("zulu")) return "Zulu";
        if (path.Contains("corretto")) return "Amazon";
        if (path.Contains("semeru") || path.Contains("ibm")) return "IBM";
        if (path.Contains("graal")) return "GraalVM";
        if (path.Contains("jdk") || path.Contains("jre")) return "Oracle";
        return "Unknown";
    }

    private static bool TryRunProperties(string javaExe, out JavaRuntime runtime)
    {
        runtime = null!;
        try
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName = javaExe,
                Arguments = "-XshowSettings:properties -version",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (process is null)
            {
                return false;
            }

            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000))
            {
                process.Kill(entireProcessTree: true);
                return false;
            }

            var version = Regex.Match(output, @"java\.version\s*=\s*(\S+)");
            var arch = Regex.Match(output, @"os\.arch\s*=\s*(\S+)");
            if (!version.Success)
            {
                return false;
            }

            runtime = new JavaRuntime(
                javaExe,
                ParseMajor(version.Groups[1].Value),
                Is64BitArch(arch.Success ? arch.Groups[1].Value : string.Empty),
                GuessVendor(javaExe),
                version.Groups[1].Value);
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }
}
