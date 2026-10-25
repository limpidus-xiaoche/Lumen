using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>右上角账号区：登录方式 pill（离线/正版）+ 皮肤头像 + 用户名。</summary>
public partial class AccountsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Username))]
    private AccountSummary? activeAccount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OnlineMode))]
    private bool offlineMode = true;

    [ObservableProperty]
    private string statusText = "离线模式";

    /// <summary>皮肤头像。M0 内置占位图（后续接 AccountSummary.AvatarUrl 的真实皮肤）。</summary>
    public IImage? Avatar { get; } = new Bitmap(
        AssetLoader.Open(new Uri("avares://Lumen/Assets/avatar_head.png")));

    /// <summary>登录方式反向：正版 = 非离线。</summary>
    public bool OnlineMode
    {
        get => !OfflineMode;
        set => OfflineMode = !value;
    }

    public string Username => ActiveAccount?.Username ?? "未登录";

    public AccountsViewModel(ILauncherFacade facade)
    {
        _facade = facade;
        _ = LoadActiveAsync();
    }

    private async Task LoadActiveAsync()
    {
        var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
        ActiveAccount = snapshot.Accounts.FirstOrDefault(a => a.Id == snapshot.ActiveAccountId)
                        ?? snapshot.Accounts.FirstOrDefault();
    }

    /// <summary>离线登录（M0 Fake 直接成功）。</summary>
    [RelayCommand]
    private async Task LoginOfflineAsync()
    {
        var account = await _facade.LoginAsync(
            new LoginRequest(AuthMode.Offline, "Player", null, null),
            new Progress<LoginEvent>(),
            CancellationToken.None);
        ActiveAccount = account;
        StatusText = $"已登录：{account.Username}";
    }

    /// <summary>正版登录入口（M0 占位，Core 未就绪）。</summary>
    [RelayCommand]
    private void SelectMicrosoft()
    {
        OfflineMode = false;
        StatusText = "正版登录将在 M4 接入（Core 未就绪）";
    }
}
