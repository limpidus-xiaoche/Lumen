namespace Launcher.Core.Install;

public sealed class InstallException : Exception
{
    public InstallException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
