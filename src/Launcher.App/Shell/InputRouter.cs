using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Launcher.App.Shell;

/// <summary>
/// 单一返回栈：Modal → 面板 → 全屏 → 退出确认。
/// S1 只有全屏与退出确认两级，模态/面板在后续阶段接入同一栈。
/// </summary>
public sealed class InputRouter
{
    private readonly Stack<Func<bool>> _handlers = new();

    public void Push(Func<bool> handler) => _handlers.Push(handler);

    public bool Back()
    {
        while (_handlers.Count > 0)
        {
            var handler = _handlers.Peek();
            if (handler())
            {
                return true;
            }

            _handlers.Pop();
        }

        return false;
    }

    public static void Attach(Window window, InputRouter router, Action onQuitRequested)
    {
        router.Push(() =>
        {
            if (window.WindowState != WindowState.FullScreen)
            {
                return false;
            }

            ExitFullScreen(window);
            return true;
        });

        window.KeyDown += (_, e) =>
        {
            switch (e.Key)
            {
                case Key.Escape:
                    if (!router.Back())
                    {
                        onQuitRequested();
                    }

                    e.Handled = true;
                    break;
                case Key.F11:
                    if (window.WindowState == WindowState.FullScreen)
                    {
                        ExitFullScreen(window);
                    }
                    else
                    {
                        EnterFullScreen(window);
                    }

                    e.Handled = true;
                    break;
            }
        };
    }

    public static void EnterFullScreen(Window window)
    {
        window.WindowState = WindowState.FullScreen;
        window.WindowDecorations = WindowDecorations.None;
        window.CanResize = false;
    }

    /// <summary>退出全屏必须恢复装饰，否则会得到不可移动、无关闭按钮的窗口。</summary>
    public static void ExitFullScreen(Window window)
    {
        window.WindowState = WindowState.Normal;
        window.WindowDecorations = WindowDecorations.Full;
        window.CanResize = true;
        ClampToWorkArea(window);
    }

    private static void ClampToWorkArea(Window window)
    {
        var screen = window.Screens.ScreenFromWindow(window) ?? window.Screens.Primary;
        if (screen is null)
        {
            return;
        }

        var area = screen.WorkingArea;
        var width = (int)Math.Min(window.Width, area.Width);
        var height = (int)Math.Min(window.Height, area.Height);
        var x = Math.Clamp((int)window.Position.X, area.X, area.X + Math.Max(0, area.Width - width));
        var y = Math.Clamp((int)window.Position.Y, area.Y, area.Y + Math.Max(0, area.Height - height));

        window.Width = width;
        window.Height = height;
        window.Position = new PixelPoint(x, y);
    }
}
