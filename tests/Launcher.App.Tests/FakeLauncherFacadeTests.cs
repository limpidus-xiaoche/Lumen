using Launcher.App.Mock;
using Launcher.Protocol;
using Xunit;

namespace Launcher.App.Tests;

public sealed class FakeLauncherFacadeTests
{
    [Fact]
    public async Task Snapshot_Has_Fake_Instances()
    {
        var facade = new FakeLauncherFacade();
        var snapshot = await facade.GetSnapshotAsync(CancellationToken.None);

        Assert.NotEmpty(snapshot.Instances);
        Assert.Equal("0.1.0-m0", snapshot.AppVersion);
    }

    [Fact]
    public async Task Offline_Login_Works()
    {
        var facade = new FakeLauncherFacade();
        var account = await facade.LoginAsync(
            new LoginRequest(AuthMode.Offline, "Player", null, null),
            new Progress<LoginEvent>(),
            CancellationToken.None);

        Assert.Equal("Player", account.Username);
        Assert.Equal(AuthMode.Offline, account.Type);
    }
}
