using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Launcher.App.ViewModels;

namespace Launcher.App.Views;

public partial class AccountsView : UserControl
{
    public AccountsView()
    {
        InitializeComponent();
    }

    /// <summary>复制微软设备码到剪贴板。</summary>
    private async void OnCopyCodeClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AccountsViewModel { DeviceUserCode: { } code } viewModel)
        {
            return;
        }

        if (TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(code);
            viewModel.LoginStatus = "设备代码已复制";
        }
    }

    /// <summary>在系统浏览器打开微软验证链接。</summary>
    private void OnOpenVerifyClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AccountsViewModel { DeviceVerificationUrl: { } url } viewModel)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            viewModel.LoginStatus = $"无法打开浏览器：{ex.Message}";
        }
    }
}
