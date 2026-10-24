namespace Launcher.Core.Launch;

public sealed class LaunchException : Exception
{
    public LaunchException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
