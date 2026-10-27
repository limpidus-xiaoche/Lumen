using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Launcher.App.Audio;
using Launcher.App.ViewModels;

namespace Launcher.App.Views;

public partial class HomeView : UserControl
{
    public HomeView()
    {
        InitializeComponent();

        // 启动走地狱门传送音效；停止用末地烛轻响，避免误触时仍播启动音。
        LaunchButton.Click += (_, _) =>
            SoundFx.Play(DataContext is HomeViewModel { IsRunning: true } ? SoundFx.Kind.Chime : SoundFx.Kind.Portal);
    }

    /// <summary>导入本地 PNG 皮肤（64×64 / 64×32），校验在 ViewModel 内完成。</summary>
    private async void OnImportSkinClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not HomeViewModel viewModel)
        {
            return;
        }

        var top = TopLevel.GetTopLevel(this);
        if (top is null)
        {
            return;
        }

        var files = await top.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "选择皮肤 PNG",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("皮肤 PNG")
                {
                    Patterns = ["*.png"],
                    MimeTypes = ["image/png"],
                },
            ],
        });

        if (files.FirstOrDefault() is not { } file)
        {
            return;
        }

        try
        {
            await using var stream = await file.OpenReadAsync();
            viewModel.SetSkin(new Bitmap(stream));
        }
        catch
        {
            viewModel.StatusText = "皮肤文件无法读取";
        }
    }
}
