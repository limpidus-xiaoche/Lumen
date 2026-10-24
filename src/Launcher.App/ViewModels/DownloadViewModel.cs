using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Launcher.App.ViewModels;

public partial class DownloadViewModel : ObservableObject
{
    public ObservableCollection<string> Versions { get; } =
    [
        "1.21.1（M0 占位）",
        "1.20.1（M0 占位）",
        "1.12.2（M0 占位）",
    ];

    [ObservableProperty]
    private string statusText = "下载功能将在 M1/M2 接入";
}
