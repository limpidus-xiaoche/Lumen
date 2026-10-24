namespace Launcher.Core.Update;

/// <summary>自身完整性校验失败（E8002）。</summary>
public sealed class IntegrityException : Exception
{
    public IntegrityException(string message, IReadOnlyList<string>? failedFiles = null, Exception? inner = null)
        : base(message, inner)
    {
        FailedFiles = failedFiles ?? Array.Empty<string>();
    }

    public string ErrorCode => "E8002";

    public IReadOnlyList<string> FailedFiles { get; }
}
