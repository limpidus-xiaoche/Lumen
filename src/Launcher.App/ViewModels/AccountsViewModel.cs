using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class AccountsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    [ObservableProperty]
    private string username = "Player";

    [ObservableProperty]
    private string statusText = "尚未登录";

    public AccountsViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    [RelayCommand]
    private async Task LoginOfflineAsync()
    {
        var account = await _facade.LoginAsync(
            new LoginRequest(AuthMode.Offline, Username, ServerUrl: null, Password: null),
            new Progress<LoginEvent>(),
            CancellationToken.None);

        StatusText = $"已登录：{account.Username} ({account.Uuid})";
    }
}
