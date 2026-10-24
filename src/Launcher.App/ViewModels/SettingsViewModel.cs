using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    public ObservableCollection<JavaRuntime> Javas { get; } = [];

    [ObservableProperty]
    private int maxMemoryMb = 4096;

    [ObservableProperty]
    private string statusText = "设置尚未保存";

    [ObservableProperty]
    private string javaStatusText = "尚未检测 Java";

    public SettingsViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _facade.UpdateSettingsAsync(new AppSettingsPatch(MaxMemoryMb: MaxMemoryMb), CancellationToken.None);
        StatusText = $"已保存：最大内存 {MaxMemoryMb} MB";
    }

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
}
