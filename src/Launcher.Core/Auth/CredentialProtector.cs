using System.Security.Cryptography;

namespace Launcher.Core.Auth;

public interface ICredentialProtector
{
    byte[] Protect(byte[] plaintext);
    byte[] Unprotect(byte[] ciphertext);
}

/// <summary>
/// Windows DPAPI（CurrentUser）。换机后必然无法解密，调用方应视为 token 失效并要求重新登录。
/// </summary>
public sealed class DpapiCredentialProtector : ICredentialProtector
{
    public byte[] Protect(byte[] plaintext) =>
        ProtectedData.Protect(plaintext, optionalEntropy: null, DataProtectionScope.CurrentUser);

    public byte[] Unprotect(byte[] ciphertext) =>
        ProtectedData.Unprotect(ciphertext, optionalEntropy: null, DataProtectionScope.CurrentUser);
}
