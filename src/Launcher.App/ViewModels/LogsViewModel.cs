using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class LogsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    public ObservableCollection<string> Files { get; } = [];

    [ObservableProperty]
    private string statusText = "尚未加载";

    public LogsViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var logs = await _facade.GetLogFilesAsync(instanceId: null, CancellationToken.None);
        Files.Clear();
        foreach (var log in logs)
        {
            Files.Add($"{log.Kind} · {log.Size} B · {log.Path}");
        }

        StatusText = $"{Files.Count} 个日志文件（Fake）";
    }
}
