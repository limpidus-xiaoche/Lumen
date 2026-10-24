using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class DownloadViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    public ObservableCollection<VersionSummary> Versions { get; } = [];

    [ObservableProperty]
    private string latestRelease = "-";

    [ObservableProperty]
    private string statusText = "尚未加载版本列表";

    [ObservableProperty]
    private bool isBusy;

    public DownloadViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            var manifest = await _facade.RefreshManifestAsync(ManifestRefreshMode.IfStale, CancellationToken.None);
            LatestRelease = manifest.LatestRelease;
            Versions.Clear();
            foreach (var version in manifest.Versions)
            {
                Versions.Add(version);
            }

            StatusText = $"共 {Versions.Count} 个版本（最新正式版 {LatestRelease}）";
        }
        catch (Exception ex)
        {
            StatusText = $"加载失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
