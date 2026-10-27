using Avalonia;
using Avalonia.Threading;

namespace Launcher.App.Services;

/// <summary>
/// 把回调切回 UI 线程；单元测试没有 Avalonia 应用时直接在当前线程执行。
/// </summary>
public static class UiThread
{
    public static void Post(Action action)
    {
        try
        {
            if (Application.Current is null)
            {
                action();
                return;
            }

            if (Dispatcher.UIThread.CheckAccess())
            {
                action();
            }
            else
            {
                Dispatcher.UIThread.Post(action);
            }
        }
        catch
        {
            action();
        }
    }
}
