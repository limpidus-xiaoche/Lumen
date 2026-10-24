namespace Launcher.Core.Auth;

public sealed class AuthException : Exception
{
    public AuthException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}
