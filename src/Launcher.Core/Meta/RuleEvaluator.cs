using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace Launcher.Core.Meta;

public sealed class RuleContext
{
    public string OsName { get; init; } = OperatingSystem.IsWindows() ? "windows" : "linux";
    public string OsArch { get; init; } = RuntimeInformation.ProcessArchitecture switch
    {
        Architecture.X86 => "x86",
        Architecture.X64 => "x86_64",
        Architecture.Arm64 => "arm64",
        _ => "unknown",
    };

    public string OsVersion { get; init; } = Environment.OSVersion.Version.ToString();
    public IReadOnlyDictionary<string, bool> Features { get; init; } =
        new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
}

public static class RuleEvaluator
{
    public static bool IsAllowed(IEnumerable<Rule>? rules, RuleContext? context = null)
    {
        if (rules is null)
        {
            return true;
        }

        var list = rules.ToList();
        if (list.Count == 0)
        {
            return true;
        }

        context ??= new RuleContext();
        var allowed = false;

        foreach (var rule in list)
        {
            if (!Matches(rule, context))
            {
                continue;
            }

            allowed = string.Equals(rule.Action, "allow", StringComparison.OrdinalIgnoreCase);
        }

        return allowed;
    }

    public static bool Matches(Rule rule, RuleContext context)
    {
        if (rule.Os is not null)
        {
            if (!string.IsNullOrEmpty(rule.Os.Name) &&
                !string.Equals(rule.Os.Name, context.OsName, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(rule.Os.Arch) &&
                !string.Equals(rule.Os.Arch, context.OsArch, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(rule.Os.Version) &&
                !Regex.IsMatch(context.OsVersion, rule.Os.Version))
            {
                return false;
            }
        }

        if (rule.Features is not null)
        {
            foreach (var feature in rule.Features)
            {
                if (!context.Features.TryGetValue(feature.Key, out var actual) || actual != feature.Value)
                {
                    return false;
                }
            }
        }

        return true;
    }
}
