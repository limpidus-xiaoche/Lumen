using System.Globalization;
using System.Text;

namespace Launcher.Core.Launch;

public static class ArgFileWriter
{
    private static readonly object Sync = new();

    static ArgFileWriter()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public static Encoding GetEncoding(int javaMajor) =>
        javaMajor >= 18
            ? new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
            : Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.ANSICodePage);

    public static string Write(IReadOnlyList<string> args, int javaMajor)
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen");
        Directory.CreateDirectory(root);
        var dir = Path.Combine(root, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "args.txt");

        lock (Sync)
        {
            File.WriteAllLines(path, args.Select(Escape), GetEncoding(javaMajor));
        }

        return path;
    }

    public static string Escape(string arg)
    {
        if (arg.Length == 0)
        {
            return "\"\"";
        }

        var needsQuote = arg.Any(char.IsWhiteSpace) || arg.Contains('"') || arg.Contains('#');
        if (!needsQuote)
        {
            return arg;
        }

        return "\"" + arg.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    public static void DeleteWithRetry(string path)
    {
        for (var i = 0; i < 5; i++)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                return;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
            catch (UnauthorizedAccessException)
            {
                return;
            }
        }
    }

    public static void SweepOld(TimeSpan maxAge)
    {
        var root = Path.Combine(Path.GetTempPath(), "lumen");
        if (!Directory.Exists(root))
        {
            return;
        }

        var cutoff = DateTime.UtcNow - maxAge;
        foreach (var directory in Directory.EnumerateDirectories(root))
        {
            try
            {
                if (Directory.GetCreationTimeUtc(directory) < cutoff)
                {
                    Directory.Delete(directory, recursive: true);
                }
            }
            catch (Exception)
            {
                // 残留文件交给下次清扫，不影响启动。
            }
        }
    }
}
