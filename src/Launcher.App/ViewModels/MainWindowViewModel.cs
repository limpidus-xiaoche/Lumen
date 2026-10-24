using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public sealed record NavItem(string Title, object ViewModel);

public partial class MainWindowViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;
    private readonly InstancesViewModel _instances;
    private readonly LogsViewModel _logs;
    private readonly MoreViewModel _more;

    /// <summary>右上角账号区（PCL2 风格：登录方式 pill + 皮肤头像 + 用户名）。</summary>
    public AccountsViewModel Accounts { get; }

    /// <summary>左侧三大导航按钮：启动 / 下载 / 设置。</summary>
    public ObservableCollection<NavItem> Pages { get; }

    [ObservableProperty]
    private NavItem? selectedPage;

    [ObservableProperty]
    private object? currentPage;

    public MainWindowViewModel(ILauncherFacade facade)
    {
        _facade = facade;

        Accounts = new AccountsViewModel(facade);
        _instances = new InstancesViewModel(facade);
        _logs = new LogsViewModel(facade);
        _more = new MoreViewModel(facade);

        var home = new HomeViewModel(facade, ShowSubPage, ShowHome);
        var download = new DownloadViewModel(facade);
        var settings = new SettingsViewModel(facade, _instances, _logs, ShowSubPage);

        Pages =
        [
            new NavItem("启动", home),
            new NavItem("下载", download),
            new NavItem("设置", settings),
        ];

        SelectedPage = Pages[0];
    }

    /// <summary>设置页内的次级入口（实例管理 / 查看日志）用。</summary>
    private void ShowSubPage(object viewModel) => CurrentPage = viewModel;

    /// <summary>从次级页面返回启动页。</summary>
    private void ShowHome() => CurrentPage = Pages[0].ViewModel;

    /// <summary>打开「更多」页（关于 / 免责声明 / 功能对接），并取消左侧导航高亮。</summary>
    [RelayCommand]
    private void ShowMore()
    {
        SelectedPage = null;
        CurrentPage = _more;
    }

    partial void OnSelectedPageChanged(NavItem? value)
    {
        if (value is not null)
        {
            CurrentPage = value.ViewModel;
        }
    }
}
