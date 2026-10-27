using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;
using Launcher.Core.Auth;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>登录方式的界面文案。</summary>
public static class AccountDisplay
{
    public static string TypeLabel(AuthMode mode) => mode switch
    {
        AuthMode.Offline => "离线",
        AuthMode.Microsoft => "微软",
        AuthMode.Authlib => "外置",
        _ => mode.ToString(),
    };
}

/// <summary>账户列表的一行：头像、用户名、类型标签，支持设为当前 / 删除。</summary>
public sealed partial class AccountRowViewModel : ObservableObject
{
    private readonly AccountsViewModel _owner;

    public AccountRowViewModel(AccountsViewModel owner, AccountSummary account)
    {
        _owner = owner;
        Id = account.Id;
        Username = account.Username;
        Type = account.Type;
        IsActive = account.IsActive;
    }

    public string Id { get; }

    public string Username { get; }

    public AuthMode Type { get; }

    public string TypeText => AccountDisplay.TypeLabel(Type);

    public string ActiveText => "当前";

    public bool CanActivate => !IsActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanActivate))]
    private bool isActive;

    [ObservableProperty]
    private bool isBusy;

    [RelayCommand]
    private Task ActivateAsync() => _owner.ActivateAsync(this);

    [RelayCommand]
    private Task RemoveAsync() => _owner.RemoveAsync(this);
}

/// <summary>
/// 账户页：账户列表 + 三种登录方式（离线 / 微软设备码 / 外置 authlib）。
/// 同时向标题栏与首页账号卡提供活动账户的用户名、类型与头像。
/// </summary>
public partial class AccountsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;
    private readonly IImage? _avatar;
    private CancellationTokenSource? _loginCts;

    public ObservableCollection<AccountRowViewModel> Items { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Username))]
    [NotifyPropertyChangedFor(nameof(AccountSubtitle))]
    [NotifyPropertyChangedFor(nameof(IsLoggedIn))]
    private AccountSummary? activeAccount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OfflineMode))]
    [NotifyPropertyChangedFor(nameof(OnlineMode))]
    [NotifyPropertyChangedFor(nameof(MicrosoftMode))]
    [NotifyPropertyChangedFor(nameof(AuthlibMode))]
    [NotifyPropertyChangedFor(nameof(IsOfflineForm))]
    [NotifyPropertyChangedFor(nameof(IsMicrosoftForm))]
    [NotifyPropertyChangedFor(nameof(IsAuthlibForm))]
    [NotifyPropertyChangedFor(nameof(CanCancelLogin))]
    private AuthMode loginMode = AuthMode.Offline;

    [ObservableProperty]
    private string offlineUsername = "Player";

    [ObservableProperty]
    private string authlibServerUrl = string.Empty;

    [ObservableProperty]
    private string authlibUsername = string.Empty;

    [ObservableProperty]
    private string authlibPassword = string.Empty;

    [ObservableProperty]
    private string loginStatus = "账户会保存在本机；未登录时默认使用 Steve 头像。";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanCancelLogin))]
    private bool isLoggingIn;

    [ObservableProperty]
    private bool hasDeviceCode;

    [ObservableProperty]
    private string? deviceUserCode;

    [ObservableProperty]
    private string? deviceVerificationUrl;

    [ObservableProperty]
    private string deviceMessage = string.Empty;

    [ObservableProperty]
    private bool isEmpty;

    public AccountsViewModel(ILauncherFacade facade)
    {
        _facade = facade;
        _avatar = LoadAvatar();
        _ = LoadAsync();
    }

    /// <summary>皮肤头像（未登录为默认 Steve）。</summary>
    public IImage? Avatar => _avatar;

    public string Username => ActiveAccount?.Username ?? "未登录";

    public string AccountSubtitle => ActiveAccount is { } account
        ? $"{AccountDisplay.TypeLabel(account.Type)}账户 · 点击管理"
        : "未登录 · 点击登录";

    public bool IsLoggedIn => ActiveAccount is not null;

    public string AccountCountText => Items.Count == 0 ? "暂无账户" : $"共 {Items.Count} 个账户";

    public bool OfflineMode
    {
        get => LoginMode == AuthMode.Offline;
        set
        {
            if (value)
            {
                LoginMode = AuthMode.Offline;
            }
        }
    }

    /// <summary>首页「正版」pill：对应微软登录。</summary>
    public bool OnlineMode
    {
        get => LoginMode == AuthMode.Microsoft;
        set
        {
            if (value)
            {
                LoginMode = AuthMode.Microsoft;
            }
        }
    }

    public bool MicrosoftMode
    {
        get => LoginMode == AuthMode.Microsoft;
        set
        {
            if (value)
            {
                LoginMode = AuthMode.Microsoft;
            }
        }
    }

    public bool AuthlibMode
    {
        get => LoginMode == AuthMode.Authlib;
        set
        {
            if (value)
            {
                LoginMode = AuthMode.Authlib;
            }
        }
    }

    public bool IsOfflineForm => LoginMode == AuthMode.Offline;

    public bool IsMicrosoftForm => LoginMode == AuthMode.Microsoft;

    public bool IsAuthlibForm => LoginMode == AuthMode.Authlib;

    public bool CanCancelLogin => IsLoggingIn && LoginMode == AuthMode.Microsoft;

    /// <summary>读取账户列表与活动账户（打开账户页 / 登录完成后调用）。</summary>
    public async Task LoadAsync()
    {
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            var activeId = snapshot.ActiveAccountId;

            Items.Clear();
            foreach (var account in snapshot.Accounts)
            {
                Items.Add(new AccountRowViewModel(this, account with { IsActive = account.Id == activeId }));
            }

            ActiveAccount = snapshot.Accounts.FirstOrDefault(a => a.Id == activeId)
                            ?? snapshot.Accounts.FirstOrDefault();
            IsEmpty = Items.Count == 0;
            OnPropertyChanged(nameof(AccountCountText));
        }
        catch (Exception ex)
        {
            LoginStatus = $"读取账户失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    /// <summary>离线登录：用户名可自定义，留空回退 Player。</summary>
    [RelayCommand]
    private async Task LoginOfflineAsync()
    {
        if (IsLoggingIn)
        {
            return;
        }

        LoginMode = AuthMode.Offline;
        var name = string.IsNullOrWhiteSpace(OfflineUsername) ? "Player" : OfflineUsername.Trim();
        IsLoggingIn = true;
        LoginStatus = "正在登录离线账户…";
        try
        {
            var account = await _facade.LoginAsync(
                new LoginRequest(AuthMode.Offline, name, null, null),
                new Progress<LoginEvent>(),
                CancellationToken.None);
            OfflineUsername = account.Username;
            await LoadAsync();
            LoginStatus = $"已登录：{account.Username}";
        }
        catch (Exception ex)
        {
            LoginStatus = $"离线登录失败：{DescribeError(ex)}";
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    /// <summary>微软登录：展示设备码与验证链接，等待用户在浏览器完成授权。</summary>
    [RelayCommand]
    private async Task LoginMicrosoftAsync()
    {
        if (IsLoggingIn)
        {
            return;
        }

        LoginMode = AuthMode.Microsoft;
        _loginCts = new CancellationTokenSource();
        IsLoggingIn = true;
        HasDeviceCode = false;
        DeviceUserCode = null;
        DeviceVerificationUrl = null;
        DeviceMessage = string.Empty;
        LoginStatus = "正在向 Microsoft 申请设备码…";

        try
        {
            var progress = new Progress<LoginEvent>(e => UiThread.Post(() => OnLoginEvent(e)));
            var account = await _facade.LoginAsync(
                new LoginRequest(AuthMode.Microsoft, null, null, null),
                progress,
                _loginCts.Token);
            await LoadAsync();
            HasDeviceCode = false;
            LoginStatus = $"已登录：{account.Username}";
        }
        catch (OperationCanceledException)
        {
            LoginStatus = "已取消 Microsoft 登录";
        }
        catch (Exception ex)
        {
            LoginStatus = $"Microsoft 登录失败：{DescribeError(ex)}";
        }
        finally
        {
            IsLoggingIn = false;
            HasDeviceCode = false;
            _loginCts.Dispose();
            _loginCts = null;
        }
    }

    /// <summary>外置登录：验证服务器地址 + 用户名 + 密码（缺失时提示 E6005）。</summary>
    [RelayCommand]
    private async Task LoginAuthlibAsync()
    {
        if (IsLoggingIn)
        {
            return;
        }

        LoginMode = AuthMode.Authlib;
        if (string.IsNullOrWhiteSpace(AuthlibServerUrl) ||
            string.IsNullOrWhiteSpace(AuthlibUsername) ||
            string.IsNullOrWhiteSpace(AuthlibPassword))
        {
            LoginStatus = "E6005：外置登录需要服务器地址、用户名与密码。";
            return;
        }

        IsLoggingIn = true;
        LoginStatus = "正在向外置服务器验证…";
        try
        {
            var account = await _facade.LoginAsync(
                new LoginRequest(AuthMode.Authlib, AuthlibUsername.Trim(), AuthlibServerUrl.Trim(), AuthlibPassword),
                new Progress<LoginEvent>(),
                CancellationToken.None);
            AuthlibPassword = string.Empty;
            await LoadAsync();
            LoginStatus = $"已登录：{account.Username}";
        }
        catch (Exception ex)
        {
            LoginStatus = $"外置登录失败：{DescribeError(ex)}";
        }
        finally
        {
            IsLoggingIn = false;
        }
    }

    [RelayCommand]
    private void CancelLogin()
    {
        _loginCts?.Cancel();
        LoginStatus = "正在取消 Microsoft 登录…";
    }

    internal async Task ActivateAsync(AccountRowViewModel row)
    {
        if (row.IsBusy || row.IsActive)
        {
            return;
        }

        row.IsBusy = true;
        try
        {
            await _facade.SetActiveAccountAsync(row.Id, CancellationToken.None);
            await LoadAsync();
            LoginStatus = $"已切换为 {row.Username}";
        }
        catch (Exception ex)
        {
            LoginStatus = $"切换失败：{DescribeError(ex)}";
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    internal async Task RemoveAsync(AccountRowViewModel row)
    {
        if (row.IsBusy)
        {
            return;
        }

        row.IsBusy = true;
        try
        {
            await _facade.LogoutAsync(row.Id, CancellationToken.None);
            await LoadAsync();
            LoginStatus = $"已删除账户：{row.Username}";
        }
        catch (Exception ex)
        {
            LoginStatus = $"删除失败：{DescribeError(ex)}";
        }
        finally
        {
            row.IsBusy = false;
        }
    }

    private void OnLoginEvent(LoginEvent e)
    {
        switch (e.Kind)
        {
            case LoginEventKind.DeviceCode:
                DeviceUserCode = e.UserCode;
                DeviceVerificationUrl = e.VerificationUrl;
                DeviceMessage = e.Message;
                HasDeviceCode = true;
                LoginStatus = "请在浏览器完成 Microsoft 登录";
                break;

            case LoginEventKind.WaitingForUser:
                LoginStatus = e.Message;
                break;

            case LoginEventKind.Failed:
                LoginStatus = $"登录失败：{e.Message}";
                break;

            case LoginEventKind.Succeeded:
                LoginStatus = e.Message;
                break;
        }
    }

    private static string DescribeError(Exception ex) => ex switch
    {
        AuthException auth => $"{auth.ErrorCode}：{auth.Message}",
        OperationCanceledException => "登录已取消",
        _ => ex.Message,
    };

    private static IImage? LoadAvatar()
    {
        try
        {
            return new Bitmap(AssetLoader.Open(new Uri("avares://Lumen/Assets/avatar_head.png")));
        }
        catch
        {
            // 单元测试等无 Avalonia 资源宿主的场景：不提供头像也能正常工作。
            return null;
        }
    }
}
