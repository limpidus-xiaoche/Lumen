using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Launcher.App.ViewModels;

namespace Launcher.App.Views;

public partial class VersionSettingsView : UserControl
{
    public VersionSettingsView()
    {
        InitializeComponent();
    }

    /// <summary>添加模组：选择一个或多个 jar，交给门面复制到 mods 目录。</summary>
    private async void OnAddModClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not VersionSettingsViewModel viewModel)
        {
            return;
        }

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return;
        }

        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择模组文件",
            AllowMultiple = true,
            FileTypeFilter = [new FilePickerFileType("模组文件") { Patterns = ["*.jar"] }],
        });

        foreach (var file in files)
        {
            var path = file.TryGetLocalPath();
            if (path is not null)
            {
                await viewModel.AddModAsync(path);
            }
        }
    }
}
