using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public sealed record NavItem(string Title, object ViewModel);

public partial class MainWindowViewModel : ObservableObject
{
    public ObservableCollection<NavItem> Pages { get; }

    [ObservableProperty]
    private NavItem? selectedPage;

    [ObservableProperty]
    private object? currentPage;

    public MainWindowViewModel(ILauncherFacade facade)
    {
        Pages =
        [
            new NavItem("首页", new HomeViewModel(facade)),
            new NavItem("实例", new InstancesViewModel(facade)),
            new NavItem("下载", new DownloadViewModel()),
            new NavItem("账户", new AccountsViewModel(facade)),
            new NavItem("设置", new SettingsViewModel(facade)),
            new NavItem("日志", new LogsViewModel(facade)),
        ];

        SelectedPage = Pages[0];
    }

    partial void OnSelectedPageChanged(NavItem? value)
    {
        if (value is not null)
        {
            CurrentPage = value.ViewModel;
        }
    }
}
