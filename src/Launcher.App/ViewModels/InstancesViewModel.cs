using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class InstancesViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;

    public ObservableCollection<InstanceSummary> Instances { get; } = [];

    [ObservableProperty]
    private string statusText = "尚未加载";

    public InstancesViewModel(ILauncherFacade facade)
    {
        _facade = facade;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
        Instances.Clear();
        foreach (var instance in snapshot.Instances)
        {
            Instances.Add(instance);
        }

        StatusText = $"{Instances.Count} 个实例";
    }
}
