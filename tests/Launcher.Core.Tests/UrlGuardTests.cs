using System.Net;
using Launcher.Core.Download;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class UrlGuardTests
{
    [Fact]
    public void Rejects_Http()
    {
        var ex = Assert.Throws<DownloadException>(() =>
            UrlGuard.Validate("http://piston-meta.mojang.com/x"));
        Assert.Equal("E2001", ex.ErrorCode);
    }

    [Fact]
    public void Rejects_Unknown_Host()
    {
        Assert.Throws<DownloadException>(() =>
            UrlGuard.Validate("https://example.com/x"));
    }

    [Fact]
    public void Allows_Loopback_Option_For_Tests()
    {
        UrlGuard.Validate("https://127.0.0.1/x", allowLoopback: true);
    }

    [Fact]
    public void Rejects_Private_Ip()
    {
        Assert.True(UrlGuard.IsPrivate(IPAddress.Parse("10.0.0.1")));
        Assert.True(UrlGuard.IsPrivate(IPAddress.Parse("172.16.0.1")));
        Assert.True(UrlGuard.IsPrivate(IPAddress.Parse("192.168.1.1")));
        Assert.True(UrlGuard.IsPrivate(IPAddress.Parse("127.0.0.1")));
        Assert.True(UrlGuard.IsPrivate(IPAddress.Parse("169.254.1.1")));
        Assert.False(UrlGuard.IsPrivate(IPAddress.Parse("1.1.1.1")));
    }
}
