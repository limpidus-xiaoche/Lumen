using Avalonia.Controls;
using Avalonia.Input;

namespace Launcher.App;

public partial class MainWindow : Window
{
    private bool _classic;

    public MainWindow()
    {
        InitializeComponent();
        Immersive.ClassicViewRequested += (_, _) => ToggleShell();
        KeyDown += OnKeyDown;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.F2)
        {
            return;
        }

        ToggleShell();
        e.Handled = true;
    }

    /// <summary>F2 在沉浸式与经典视图之间切换；经典视图恢复普通窗口装饰与可缩放。</summary>
    public void ToggleShell()
    {
        _classic = !_classic;
        Immersive.IsVisible = !_classic;
        Classic.IsVisible = _classic;

        if (_classic)
        {
            WindowDecorations = WindowDecorations.BorderOnly;
            CanResize = true;
            WindowState = WindowState.Normal;
            Width = 1100;
            Height = 700;
        }
        else
        {
            WindowDecorations = WindowDecorations.None;
            CanResize = false;
            WindowState = WindowState.FullScreen;
        }
    }
}
