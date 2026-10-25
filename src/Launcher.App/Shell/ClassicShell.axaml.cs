using Avalonia.Controls;

namespace Launcher.App.Shell;

/// <summary>
/// 经典 PCL 风格布局（原 MainWindow 内容），由 MainWindow 在后续 S 阶段按需切换显示。
/// </summary>
public partial class ClassicShell : UserControl
{
    public ClassicShell()
    {
        InitializeComponent();

        BtnMin.Click += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is Window window)
            {
                window.WindowState = WindowState.Minimized;
            }
        };

        BtnClose.Click += (_, _) => (TopLevel.GetTopLevel(this) as Window)?.Close();
    }
}
