using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Launcher.Core.Auth;

public sealed record OfflineIdentity(string Username, string Uuid, string AccessToken);

public static class OfflineAuth
{
    public static OfflineIdentity Create(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("离线用户名不能为空。", nameof(username));
        }

        var uuid = CreateOfflineUuid(username);
        var token = CreatePseudoToken(username, uuid);
        return new OfflineIdentity(username, uuid, token);
    }

    public static string CreateOfflineUuid(string username)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30);
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80);
        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..]}";
    }

    public static string CreatePseudoToken(string username, string uuid)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new { alg = "none", typ = "JWT" }));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(new
        {
            sub = uuid,
            iss = "Lumen",
            name = username,
            offline = true,
        }));
        return $"{header}.{payload}.offline";
    }

    private static string Base64Url(byte[] data) =>
        Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
