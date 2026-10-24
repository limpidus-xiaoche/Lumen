using Avalonia.Controls;

namespace Launcher.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        BtnMin.Click += (_, _) => WindowState = WindowState.Minimized;
        BtnClose.Click += (_, _) => Close();
    }
}
