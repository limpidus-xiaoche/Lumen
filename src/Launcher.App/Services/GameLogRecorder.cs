using System.Text;

namespace Launcher.App.Services;

/// <summary>
/// 把一次启动的游戏输出落到 <c>logs/game</c>，供日志页回看。
/// 写盘失败不影响启动流程，只返回 null。
/// </summary>
public sealed class GameLogRecorder : IDisposable
{
    private readonly StreamWriter? _writer;

    private GameLogRecorder(StreamWriter writer, string path)
    {
        _writer = writer;
        FilePath = path;
    }

    /// <summary>落盘文件路径；创建失败时为 null。</summary>
    public string? FilePath { get; }

    public static GameLogRecorder? TryCreate(string logsDir, string instanceName)
    {
        try
        {
            var dir = Path.Combine(logsDir, "game");
            Directory.CreateDirectory(dir);
            var safeName = Sanitize(instanceName);
            var path = Path.Combine(dir, $"{DateTime.Now:yyyyMMdd-HHmmss}-{safeName}.log");
            var writer = new StreamWriter(path, append: false, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
            {
                AutoFlush = true,
            };
            return new GameLogRecorder(writer, path);
        }
        catch
        {
            return null;
        }
    }

    public void Write(string stream, string line)
    {
        try
        {
            _writer?.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{stream}] {line}");
        }
        catch
        {
            // 磁盘写入失败时静默忽略，避免影响游戏进程。
        }
    }

    public void Dispose()
    {
        try
        {
            _writer?.Dispose();
        }
        catch
        {
            // 忽略关闭异常。
        }
    }

    private static string Sanitize(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = name.Select(c => invalid.Contains(c) ? '_' : c).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrEmpty(result) ? "instance" : result[..Math.Min(result.Length, 32)];
    }
}
