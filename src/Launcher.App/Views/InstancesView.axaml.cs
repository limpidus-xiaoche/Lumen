using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Launcher.App.ViewModels;

namespace Launcher.App.Views;

public partial class InstancesView : UserControl
{
    public InstancesView()
    {
        InitializeComponent();
        DragDrop.SetAllowDrop(this, true);
        AddHandler(DragDrop.DragOverEvent, OnDragOver);
        AddHandler(DragDrop.DropEvent, OnDrop);
    }

    /// <summary>导入外部实例：选择 PCL / 原生 .minecraft / MultiMC / HMCL 目录。</summary>
    private async void OnImportInstanceClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InstancesViewModel viewModel)
        {
            return;
        }

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            return;
        }

        var folders = await storage.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "选择要导入的实例目录",
            AllowMultiple = false,
        });

        if (folders.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            await viewModel.ImportExternalAsync(path);
        }
    }

    /// <summary>导入整合包：.mrpack / .zip，类型交由安装器识别。</summary>
    private async void OnImportModpackClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InstancesViewModel viewModel)
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
            Title = "选择整合包文件",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("整合包")
                {
                    Patterns = ["*.mrpack", "*.zip"],
                },
                new FilePickerFileType("整合包清单")
                {
                    Patterns = ["modrinth.index.json", "modpack.json", "mmc-pack.json", "manifest.json"],
                },
            ],
        });

        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            await viewModel.ImportModpackAsync(path);
        }
    }

    /// <summary>删除实例：弹出保留存档确认框，确认后交给 ViewModel。</summary>
    private async void OnDeleteClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not InstancesViewModel viewModel)
        {
            return;
        }

        if (sender is not Control { DataContext: InstanceCardViewModel card })
        {
            return;
        }

        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            return;
        }

        var dialog = new DeleteInstanceDialog(card.Name);
        var confirmed = await dialog.ShowDialog<bool>(owner);
        if (confirmed)
        {
            await viewModel.DeleteInstanceAsync(card, dialog.KeepSaves);
        }
    }

    private static void OnDragOver(object? sender, DragEventArgs e)
    {
        e.DragEffects = e.DataTransfer.TryGetFiles() is { Length: > 0 }
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private async void OnDrop(object? sender, DragEventArgs e)
    {
        if (DataContext is not InstancesViewModel viewModel)
        {
            return;
        }

        var items = e.DataTransfer.TryGetFiles();
        if (items is null)
        {
            return;
        }

        foreach (var item in items)
        {
            if (item.TryGetLocalPath() is not { } path)
            {
                continue;
            }

            if (Directory.Exists(path))
            {
                await viewModel.ImportExternalAsync(path);
                continue;
            }

            if (Path.GetExtension(path).ToLowerInvariant() is ".mrpack" or ".zip")
            {
                await viewModel.ImportModpackAsync(path);
                continue;
            }

            viewModel.StatusText = $"不支持拖入的文件：{Path.GetFileName(path)}";
        }
    }
}
