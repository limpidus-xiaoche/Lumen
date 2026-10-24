namespace Launcher.Core.Launch;

public static class ExitCodeAnalyzer
{
    private const int AccessViolation = unchecked((int)0xC0000005);
    private const int IllegalInstruction = unchecked((int)0xC000001D);
    private const int StackOverflow = unchecked((int)0xC00000FD);

    public static string? Analyze(int exitCode, string gameDir)
    {
        if (exitCode == 0)
        {
            return null;
        }

        var baseHint = exitCode switch
        {
            AccessViolation => "访问冲突（常见于显卡驱动或模组冲突）",
            IllegalInstruction => "非法指令（CPU/Java 版本不匹配）",
            StackOverflow => "栈溢出（模组递归或内存参数过小）",
            _ => $"游戏异常退出（exit code {exitCode}）",
        };

        var crashHint = ReadCrashReport(gameDir);
        return crashHint is null ? baseHint : $"{baseHint}；{crashHint}";
    }

    private static string? ReadCrashReport(string gameDir)
    {
        try
        {
            var dir = Path.Combine(gameDir, "crash-reports");
            if (!Directory.Exists(dir))
            {
                return null;
            }

            var latest = new DirectoryInfo(dir)
                .EnumerateFiles("crash-*.txt")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .FirstOrDefault();
            if (latest is null)
            {
                return null;
            }

            var text = File.ReadAllText(latest.FullName);
            if (text.Contains("OutOfMemoryError", StringComparison.Ordinal))
            {
                return "崩溃报告显示内存不足";
            }

            if (text.Contains("UnsupportedClassVersionError", StringComparison.Ordinal))
            {
                return "崩溃报告显示 Java 版本过低";
            }

            if (text.Contains("ModResolutionException", StringComparison.Ordinal) ||
                text.Contains("Missing or unsupported", StringComparison.Ordinal))
            {
                return "崩溃报告显示模组依赖缺失";
            }

            return $"已生成崩溃报告：{latest.Name}";
        }
        catch (Exception)
        {
            return null;
        }
    }
}
