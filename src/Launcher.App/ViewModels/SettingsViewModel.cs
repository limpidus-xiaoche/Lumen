using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;
    private readonly InstancesViewModel _instances;
    private readonly LogsViewModel _logs;
    private readonly Action<object> _openSubPage;

    public ObservableCollection<JavaRuntime> Javas { get; } = [];

    [ObservableProperty]
    private int maxMemoryMb = 4096;

    [ObservableProperty]
    private string statusText = "设置尚未保存";

    [ObservableProperty]
    private string javaStatusText = "尚未检测 Java";

    /// <summary>当前是否为深色主题（默认深色）。</summary>
    [ObservableProperty]
    private bool isDarkTheme = true;

    public SettingsViewModel(
        ILauncherFacade facade,
        InstancesViewModel instances,
        LogsViewModel logs,
        Action<object> openSubPage)
    {
        _facade = facade;
        _instances = instances;
        _logs = logs;
        _openSubPage = openSubPage;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _facade.UpdateSettingsAsync(new AppSettingsPatch(MaxMemoryMb: MaxMemoryMb), CancellationToken.None);
        StatusText = $"已保存：最大内存 {MaxMemoryMb} MB";
    }

    /// <summary>刷新并列出本机检测到的 Java 运行时（真实门面 JavaLocator）。</summary>
    [RelayCommand]
    private async Task LoadJavasAsync()
    {
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            Javas.Clear();
            foreach (var java in snapshot.Javas)
            {
                Javas.Add(java);
            }

            JavaStatusText = $"检测到 {Javas.Count} 个 Java";
        }
        catch (Exception ex)
        {
            JavaStatusText = $"检测失败：{ex.Message}";
        }
    }

    /// <summary>次级入口：打开实例管理页。</summary>
    [RelayCommand]
    private void OpenInstances() => _openSubPage(_instances);

    /// <summary>次级入口：打开日志页。</summary>
    [RelayCommand]
    private void OpenLogs() => _openSubPage(_logs);

    /// <summary>切换深浅主题（M0 先直接改全局主题变体）。</summary>
    partial void OnIsDarkThemeChanged(bool value)
    {
        Application.Current!.RequestedThemeVariant = value
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;
    }
}
