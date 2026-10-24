namespace Launcher.Protocol;

public enum IntegrityCheckMode
{
    /// <summary>冷启动快校验：仅入口可执行文件 + 清单本身，按「版本 + mtime」缓存。</summary>
    Quick,

    /// <summary>全量校验：逐文件比对 SHA-256。</summary>
    Full,
}

public sealed record IntegrityReport(
    bool IsValid,
    IntegrityCheckMode Mode,
    int CheckedFiles,
    IReadOnlyList<string> FailedFiles,
    string Message,
    DateTimeOffset CheckedAt);
