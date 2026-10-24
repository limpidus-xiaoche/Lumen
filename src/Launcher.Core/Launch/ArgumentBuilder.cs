using System.Text.Json;
using System.Text.RegularExpressions;
using Launcher.Core.Install;
using Launcher.Core.Meta;

namespace Launcher.Core.Launch;

public sealed record LaunchCommand(IReadOnlyList<string> Args, string MainClass);

public static class ArgumentBuilder
{
    private static readonly Regex VariablePattern = new(@"\$\{([^}]+)\}", RegexOptions.Compiled);

    public static LaunchCommand Build(
        VersionDetail version,
        IReadOnlyList<string> classpath,
        string gameDir,
        string assetsRoot,
        string assetIndexId,
        string nativesDir,
        string librariesDir,
        IReadOnlyDictionary<string, string> variables,
        int memoryMb,
        IReadOnlyList<string>? extraJvmArgs,
        bool useG1)
    {
        var mainClass = version.MainClass
            ?? throw new LaunchException("E5002", "版本 JSON 缺少 mainClass");

        var map = new Dictionary<string, string>(variables, StringComparer.OrdinalIgnoreCase)
        {
            ["classpath"] = string.Join(Path.PathSeparator, classpath),
            ["classpath_separator"] = Path.PathSeparator.ToString(),
            ["library_directory"] = librariesDir,
            ["natives_directory"] = nativesDir,
            ["game_directory"] = gameDir,
            ["assets_root"] = assetsRoot,
            ["assets_index_name"] = assetIndexId,
        };

        var context = new RuleContext();
        var jvmArgs = Expand(version.Arguments?.Jvm, map, context);
        var gameArgs = Expand(version.Arguments?.Game, map, context);

        if (gameArgs.Count == 0 && !string.IsNullOrWhiteSpace(version.MinecraftArguments))
        {
            gameArgs = ExpandLegacy(version.MinecraftArguments, map);
        }

        if (jvmArgs.Count == 0)
        {
            jvmArgs.Add($"-Djava.library.path={nativesDir}");
        }

        if (!HasClasspath(jvmArgs))
        {
            jvmArgs.Add("-cp");
            jvmArgs.Add(string.Join(Path.PathSeparator, classpath));
        }

        if (!jvmArgs.Any(a => a.StartsWith("-Xmx", StringComparison.OrdinalIgnoreCase)))
        {
            jvmArgs.Insert(0, $"-Xmx{memoryMb}m");
        }

        if (!jvmArgs.Any(a => a.Contains("minecraft.launcher.brand", StringComparison.OrdinalIgnoreCase)))
        {
            jvmArgs.Add("-Dminecraft.launcher.brand=Lumen");
            jvmArgs.Add("-Dminecraft.launcher.version=0.1");
        }

        jvmArgs.Add("-Dlog4j2.formatMsgNoLookups=true");

        if (useG1 && !jvmArgs.Any(a => a.Contains("UseG1GC", StringComparison.OrdinalIgnoreCase)))
        {
            jvmArgs.Add("-XX:+UseG1GC");
            jvmArgs.Add("-XX:MaxGCPauseMillis=50");
        }

        if (extraJvmArgs is { Count: > 0 })
        {
            jvmArgs.AddRange(extraJvmArgs);
        }

        var args = new List<string>(jvmArgs) { mainClass };
        args.AddRange(gameArgs);

        foreach (var arg in args)
        {
            var unresolved = VariablePattern.Match(arg);
            if (unresolved.Success)
            {
                throw new LaunchException("E5002", $"未解析的启动变量：{unresolved.Value}");
            }
        }

        return new LaunchCommand(args, mainClass);
    }

    public static IReadOnlyList<string> BuildClasspath(
        VersionDetail version,
        string librariesDir,
        string versionJar)
    {
        var context = new RuleContext();
        var result = new List<string>();

        foreach (var library in version.Libraries)
        {
            if (!RuleEvaluator.IsAllowed(library.Rules, context))
            {
                continue;
            }

            if (library.Downloads?.Artifact is null && library.Downloads is not null)
            {
                // 仅 natives classifier 的库，不进入 classpath。
                continue;
            }

            var relative = library.Downloads?.Artifact?.Path
                ?? VanillaInstaller.MavenPath(library.Name);
            result.Add(Path.Combine(librariesDir, relative.Replace('/', Path.DirectorySeparatorChar)));
        }

        result.Add(versionJar);
        return result.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static List<string> Expand(
        List<JsonElement>? source,
        IReadOnlyDictionary<string, string> variables,
        RuleContext context)
    {
        var result = new List<string>();
        if (source is null)
        {
            return result;
        }

        foreach (var element in source)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                AddExpanded(result, element.GetString(), variables);
                continue;
            }

            if (element.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            if (element.TryGetProperty("rules", out var rulesElement))
            {
                var rules = JsonSerializer.Deserialize<List<Rule>>(rulesElement.GetRawText());
                if (!RuleEvaluator.IsAllowed(rules, context))
                {
                    continue;
                }
            }

            if (!element.TryGetProperty("value", out var value))
            {
                continue;
            }

            if (value.ValueKind == JsonValueKind.String)
            {
                AddExpanded(result, value.GetString(), variables);
            }
            else if (value.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        AddExpanded(result, item.GetString(), variables);
                    }
                }
            }
        }

        return result;
    }

    private static List<string> ExpandLegacy(
        string minecraftArguments,
        IReadOnlyDictionary<string, string> variables)
    {
        var result = new List<string>();
        foreach (var arg in minecraftArguments.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            AddExpanded(result, arg, variables);
        }

        return result;
    }

    private static void AddExpanded(
        List<string> result,
        string? value,
        IReadOnlyDictionary<string, string> variables)
    {
        if (value is null)
        {
            return;
        }

        result.Add(VariablePattern.Replace(value, match =>
            variables.TryGetValue(match.Groups[1].Value, out var replacement)
                ? replacement
                : match.Value));
    }

    private static bool HasClasspath(IReadOnlyList<string> jvmArgs)
    {
        for (var i = 0; i < jvmArgs.Count; i++)
        {
            var arg = jvmArgs[i];
            if (string.Equals(arg, "-cp", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(arg, "-classpath", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-cp=", StringComparison.OrdinalIgnoreCase) ||
                arg.StartsWith("-classpath=", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
