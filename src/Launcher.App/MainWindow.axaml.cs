using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Launcher.App.Audio;
using Launcher.App.ViewModels;
using Launcher.App.Views;

namespace Launcher.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        BtnMin.Click += (_, _) => WindowState = WindowState.Minimized;
        BtnClose.Click += (_, _) => Close();

        BtnTasks.Click += (_, _) => SoundFx.Play(SoundFx.Kind.Book);
        BtnMore.Click += (_, _) => SoundFx.Play(SoundFx.Kind.Eye);

        // 用 Tapped 而不是 SelectionChanged：重复点击当前页也能播放音效与动画
        NavList.AddHandler(InputElement.TappedEvent, OnNavTapped, RoutingStrategies.Bubble, handledEventsToo: true);
    }

    private void OnNavTapped(object? sender, TappedEventArgs e)
    {
        if (e.Source is not Visual source)
        {
            return;
        }

        if (source.FindAncestorOfType<ListBoxItem>(includeSelf: true)?.DataContext is not NavItem item)
        {
            return;
        }

        var icon = source.GetVisualDescendants().OfType<MinecraftPixelIcon>().FirstOrDefault();

        switch (item.Icon)
        {
            case IconKind.Enderman:
                // 末影人被盯视时的生气抖动 + 叫声
                SoundFx.Play(SoundFx.Kind.Enderman);
                _ = FlashAsync(icon, "angry");
                break;

            case IconKind.Shulker:
                // 潜影贝先开壳，随后吐出子弹
                SoundFx.Play(SoundFx.Kind.ShulkerOpen);
                _ = FlashAsync(icon, "hop");
                _ = PlayShulkerShotAsync();
                break;

            case IconKind.EndRod:
                SoundFx.Play(SoundFx.Kind.Chime);
                break;
        }
    }

    private static async Task PlayShulkerShotAsync()
    {
        await Task.Delay(320);
        SoundFx.Play(SoundFx.Kind.ShulkerShoot);
    }

    private static async Task FlashAsync(Control? icon, string className)
    {
        if (icon is null)
        {
            return;
        }

        icon.Classes.Remove(className);
        await Task.Yield();
        icon.Classes.Add(className);
        await Task.Delay(700);
        icon.Classes.Remove(className);
    }
}
