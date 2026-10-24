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
}
