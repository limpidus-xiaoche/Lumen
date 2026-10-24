using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    [ObservableProperty]
    private int maxMemoryMb = 4096;

    [ObservableProperty]
    private string statusText = "设置尚未保存";

    /// <summary>当前是否为深色主题（默认深色）。</summary>
    [ObservableProperty]
    private bool isDarkTheme = true;

    public SettingsViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _facade.UpdateSettingsAsync(new AppSettingsPatch(MaxMemoryMb: MaxMemoryMb), CancellationToken.None);
        StatusText = $"已保存：最大内存 {MaxMemoryMb} MB（Fake）";
    }

    /// <summary>切换深浅主题（M0 先直接改全局主题变体）。</summary>
    partial void OnIsDarkThemeChanged(bool value)
    {
        Application.Current!.RequestedThemeVariant = value
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;
    }
}
