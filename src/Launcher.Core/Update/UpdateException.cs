namespace Launcher.Core.Update;

/// <summary>更新清单 / 更新包校验失败（E8001）。</summary>
public sealed class UpdateException : Exception
{
    public UpdateException(string message, Exception? inner = null)
        : base(message, inner)
    {
    }

    public string ErrorCode => "E8001";
}
