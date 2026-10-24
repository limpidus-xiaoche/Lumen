using System.Net;
using System.Net.Sockets;

namespace Launcher.Core.Download;

public sealed class DownloadException : Exception
{
    public DownloadException(string errorCode, string message, Exception? inner = null)
        : base(message, inner)
    {
        ErrorCode = errorCode;
    }

    public string ErrorCode { get; }
}

public static class UrlGuard
{
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "piston-meta.mojang.com",
        "piston-data.mojang.com",
        "launchermeta.mojang.com",
        "resources.download.minecraft.net",
        "libraries.minecraft.net",
        "bmclapi2.bangbang93.com",
        "api.modrinth.com",
        "cdn.modrinth.com",
        "maven.minecraftforge.net",
        "maven.neoforged.net",
        "github.com",
        "api.github.com",
        "objects.githubusercontent.com",
        "raw.githubusercontent.com",
    };

    public static void Validate(string url, bool allowLoopback = false)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            throw new DownloadException("E2001", $"无效的 URL：{url}");
        }

        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
        {
            throw new DownloadException("E2001", $"仅允许 https：{url}");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            throw new DownloadException("E2001", $"URL 不允许包含 userinfo：{url}");
        }

        if (uri.Port != 443)
        {
            throw new DownloadException("E2001", $"仅允许 443 端口：{url}");
        }

        var isLoopbackHost = uri.IsLoopback ||
                             string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase);
        if (!AllowedHosts.Contains(uri.Host) && !(allowLoopback && isLoopbackHost))
        {
            throw new DownloadException("E2001", $"host 不在白名单：{uri.Host}");
        }

        IPAddress[] addresses;
        try
        {
            addresses = Dns.GetHostAddresses(uri.Host);
        }
        catch (SocketException ex)
        {
            throw new DownloadException("E2001", $"DNS 解析失败：{uri.Host}", ex);
        }

        foreach (var address in addresses)
        {
            if (IsPrivate(address) && !(allowLoopback && IPAddress.IsLoopback(address)))
            {
                throw new DownloadException("E2001", $"拒绝私有/环回地址：{uri.Host} -> {address}");
            }
        }
    }

    public static bool IsPrivate(IPAddress address)
    {
        if (IPAddress.IsLoopback(address))
        {
            return true;
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            var bytes = address.GetAddressBytes();
            return address.Equals(IPAddress.IPv6Any) ||
                   (bytes[0] & 0xFE) == 0xFC ||
                   (bytes[0] == 0xFE && (bytes[1] & 0xC0) == 0x80);
        }

        var v4 = address.GetAddressBytes();
        return v4[0] == 0 ||
               v4[0] == 10 ||
               (v4[0] == 172 && (v4[1] & 0xF0) == 16) ||
               (v4[0] == 192 && v4[1] == 168) ||
               (v4[0] == 169 && v4[1] == 254);
    }
}
