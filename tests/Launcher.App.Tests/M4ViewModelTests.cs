using Launcher.App.Mock;
using Launcher.App.ViewModels;
using Launcher.Protocol;
using Xunit;

namespace Launcher.App.Tests;

public sealed class M4ViewModelTests
{
    private static async Task WaitUntilAsync(Func<bool> condition, string message)
    {
        for (var i = 0; i < 120; i++)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(25);
        }

        Assert.True(condition(), message);
    }

    [Fact]
    public async Task Accounts_Loads_Active_Account()
    {
        var viewModel = new AccountsViewModel(new FakeLauncherFacade());
        await viewModel.LoadAsync();

        Assert.False(viewModel.IsEmpty);
        Assert.Single(viewModel.Items);
        Assert.Equal("Player", viewModel.Username);
        Assert.True(viewModel.IsLoggedIn);
        Assert.Contains("离线账户", viewModel.AccountSubtitle);
        Assert.True(viewModel.Items[0].IsActive);
        Assert.Contains("1 个账户", viewModel.AccountCountText);
    }

    [Fact]
    public async Task Accounts_Offline_Login_Uses_Custom_Name()
    {
        var viewModel = new AccountsViewModel(new FakeLauncherFacade(startSignedIn: false));
        await viewModel.LoadAsync();
        Assert.True(viewModel.IsEmpty);

        viewModel.OfflineUsername = "Alex";
        viewModel.OfflineMode = true;
        await viewModel.LoginOfflineCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Items);
        Assert.False(viewModel.IsEmpty);
        Assert.Equal("Alex", viewModel.Username);
        Assert.Equal("离线", viewModel.Items[0].TypeText);
        Assert.Contains("已登录", viewModel.LoginStatus);
    }

    [Fact]
    public async Task Accounts_Microsoft_Login_Shows_Device_Code_Then_Activates()
    {
        var viewModel = new AccountsViewModel(new FakeLauncherFacade(startSignedIn: false));
        await viewModel.LoadAsync();

        viewModel.MicrosoftMode = true;
        Assert.True(viewModel.IsMicrosoftForm);
        Assert.False(viewModel.IsOfflineForm);

        var login = viewModel.LoginMicrosoftCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => viewModel.HasDeviceCode, "设备码应显示在界面上");
        Assert.Equal("FAKE-1234", viewModel.DeviceUserCode);
        Assert.Equal("https://www.microsoft.com/link", viewModel.DeviceVerificationUrl);
        Assert.True(viewModel.IsLoggingIn);

        await login;
        Assert.False(viewModel.HasDeviceCode);
        Assert.False(viewModel.IsLoggingIn);
        Assert.Equal("Steve", viewModel.Username);
        Assert.Equal(AuthMode.Microsoft, viewModel.ActiveAccount!.Type);
        Assert.Contains("微软账户", viewModel.AccountSubtitle);
    }

    [Fact]
    public async Task Accounts_Authlib_Requires_Server_User_And_Password()
    {
        var viewModel = new AccountsViewModel(new FakeLauncherFacade(startSignedIn: false));
        await viewModel.LoadAsync();

        viewModel.AuthlibMode = true;
        await viewModel.LoginAuthlibCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.Items);
        Assert.Contains("E6005", viewModel.LoginStatus);
    }

    [Fact]
    public async Task Accounts_Authlib_Login_Adds_Account_And_Clears_Password()
    {
        var viewModel = new AccountsViewModel(new FakeLauncherFacade(startSignedIn: false));
        await viewModel.LoadAsync();

        viewModel.AuthlibMode = true;
        viewModel.AuthlibServerUrl = "https://littleskin.cn/api/yggdrasil";
        viewModel.AuthlibUsername = "LumenUser";
        viewModel.AuthlibPassword = "secret";
        await viewModel.LoginAuthlibCommand.ExecuteAsync(null);

        Assert.Single(viewModel.Items);
        Assert.Equal("LumenUser", viewModel.Username);
        Assert.Equal("外置", viewModel.Items[0].TypeText);
        Assert.Equal(string.Empty, viewModel.AuthlibPassword);
    }

    [Fact]
    public async Task Accounts_Switch_And_Remove_Updates_Active()
    {
        var viewModel = new AccountsViewModel(new FakeLauncherFacade());
        await viewModel.LoadAsync();

        viewModel.OfflineUsername = "Alex";
        await viewModel.LoginOfflineCommand.ExecuteAsync(null);
        Assert.Equal(2, viewModel.Items.Count);
        Assert.Equal("Alex", viewModel.Username);

        var original = viewModel.Items.Single(i => i.Username == "Player");
        await original.ActivateCommand.ExecuteAsync(null);
        Assert.Equal("Player", viewModel.Username);
        Assert.True(viewModel.Items.Single(i => i.Username == "Player").IsActive);

        var alex = viewModel.Items.Single(i => i.Username == "Alex");
        await alex.RemoveCommand.ExecuteAsync(null);
        Assert.Single(viewModel.Items);
        Assert.Equal("Player", viewModel.Username);
    }

    [Fact]
    public async Task Accounts_Remove_Last_Falls_Back_To_Signed_Out()
    {
        var viewModel = new AccountsViewModel(new FakeLauncherFacade());
        await viewModel.LoadAsync();

        await viewModel.Items[0].RemoveCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsEmpty);
        Assert.Null(viewModel.ActiveAccount);
        Assert.Equal("未登录", viewModel.Username);
        Assert.Contains("未登录", viewModel.AccountSubtitle);
    }

    [Fact]
    public void MainWindow_ShowAccounts_Opens_Account_Page()
    {
        var logsDir = Path.Combine(Path.GetTempPath(), "lumen-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logsDir);
        try
        {
            var viewModel = new MainWindowViewModel(new FakeLauncherFacade(), logsDir);
            viewModel.ShowAccountsCommand.Execute(null);

            Assert.Same(viewModel.Accounts, viewModel.CurrentPage);
            Assert.Null(viewModel.SelectedPage);
        }
        finally
        {
            Directory.Delete(logsDir, recursive: true);
        }
    }
}
