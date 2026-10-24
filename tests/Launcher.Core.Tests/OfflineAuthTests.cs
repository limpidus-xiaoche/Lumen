using System.Text;
using System.Text.Json;
using Launcher.Core.Auth;
using Xunit;

namespace Launcher.Core.Tests;

public sealed class OfflineAuthTests
{
    [Fact]
    public void Uuid_Is_Deterministic_And_Version3()
    {
        var a = OfflineAuth.CreateOfflineUuid("Player");
        var b = OfflineAuth.CreateOfflineUuid("Player");

        Assert.Equal(a, b);
        Assert.Matches("^[0-9a-f]{8}-[0-9a-f]{4}-3[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$", a);
    }

    [Fact]
    public void PseudoToken_Has_Three_Parts_And_Readable_Payload()
    {
        var token = OfflineAuth.CreatePseudoToken("Player", "00000000000030008000000000000000");
        var parts = token.Split('.');

        Assert.Equal(3, parts.Length);
        Assert.Equal("offline", parts[2]);

        var payload = parts[1].Replace('-', '+').Replace('_', '/');
        payload = payload.PadRight(payload.Length + (4 - payload.Length % 4) % 4, '=');
        var json = JsonSerializer.Deserialize<JsonElement>(Encoding.UTF8.GetString(Convert.FromBase64String(payload)));
        Assert.Equal("Player", json.GetProperty("name").GetString());
        Assert.True(json.GetProperty("offline").GetBoolean());
    }
}
