using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Launcher.App.Views;

/// <summary>
/// 轻量删除确认框：Avalonia 没有内置 MessageBox，这里用无边框小窗保持项目暗紫风格。
/// 结果通过 ShowDialog&lt;bool&gt; 返回，存档开关由 <see cref="KeepSaves"/> 暴露。
/// </summary>
public partial class DeleteInstanceDialog : Window
{
    public DeleteInstanceDialog()
    {
        InitializeComponent();
    }

    public DeleteInstanceDialog(string instanceName)
        : this()
    {
        MessageText.Text = $"确定要删除实例「{instanceName}」吗？";
    }

    /// <summary>是否保留存档（默认勾选）。</summary>
    public bool KeepSaves => KeepSavesCheck.IsChecked == true;

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);

    private void OnDeleteClick(object? sender, RoutedEventArgs e) => Close(true);
}
