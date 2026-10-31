using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILauncherFacade _facade;
    private readonly InstancesViewModel _instances;
    private readonly LogsViewModel _logs;
    private readonly Action<object> _openSubPage;

    public ObservableCollection<JavaRuntime> Javas { get; } = [];

    [ObservableProperty]
    private int maxMemoryMb = 4096;

    [ObservableProperty]
    private string statusText = "设置尚未保存";

    [ObservableProperty]
    private string javaStatusText = "尚未检测 Java";

    /// <summary>当前是否为深色主题（默认深色）。</summary>
    [ObservableProperty]
    private bool isDarkTheme = true;

    /// <summary>维护操作（完整性/更新/诊断）的最近结果。</summary>
    [ObservableProperty]
    private string maintenanceStatus = "尚未执行维护操作";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMaintenanceIdle))]
    [NotifyPropertyChangedFor(nameof(CanInstallUpdate))]
    private bool isMaintenanceBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallUpdate))]
    private UpdateInfo? pendingUpdate;

    public bool IsMaintenanceIdle => !IsMaintenanceBusy;

    public bool CanInstallUpdate => PendingUpdate is not null && !IsMaintenanceBusy;

    public SettingsViewModel(
        ILauncherFacade facade,
        InstancesViewModel instances,
        LogsViewModel logs,
        Action<object> openSubPage)
    {
        _facade = facade;
        _instances = instances;
        _logs = logs;
        _openSubPage = openSubPage;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _facade.UpdateSettingsAsync(new AppSettingsPatch(MaxMemoryMb: MaxMemoryMb), CancellationToken.None);
        StatusText = $"已保存：最大内存 {MaxMemoryMb} MB";
    }

    /// <summary>刷新并列出本机检测到的 Java 运行时（真实门面 JavaLocator）。</summary>
    [RelayCommand]
    private async Task LoadJavasAsync()
    {
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            Javas.Clear();
            foreach (var java in snapshot.Javas)
            {
                Javas.Add(java);
            }

            JavaStatusText = $"检测到 {Javas.Count} 个 Java";
        }
        catch (Exception ex)
        {
            JavaStatusText = $"检测失败：{ex.Message}";
        }
    }

    /// <summary>次级入口：打开实例管理页。</summary>
    [RelayCommand]
    private void OpenInstances()
    {
        _ = _instances.LoadAsync();
        _openSubPage(_instances);
    }

    /// <summary>次级入口：打开日志页。</summary>
    [RelayCommand]
    private void OpenLogs()
    {
        _ = _logs.LoadAsync();
        _openSubPage(_logs);
    }

    /// <summary>§12.1：快速/全量完整性校验（失败时门面抛 E8002）。</summary>
    [RelayCommand]
    private async Task CheckIntegrityAsync(string? mode)
    {
        if (IsMaintenanceBusy)
        {
            return;
        }

        var checkMode = string.Equals(mode, "full", StringComparison.OrdinalIgnoreCase)
            ? IntegrityCheckMode.Full
            : IntegrityCheckMode.Quick;
        IsMaintenanceBusy = true;
        MaintenanceStatus = checkMode == IntegrityCheckMode.Full
            ? "正在全量校验程序文件…"
            : "正在快速校验程序文件…";
        try
        {
            var report = await _facade.VerifyIntegrityAsync(checkMode, CancellationToken.None);
            MaintenanceStatus = $"{report.Message}（检查 {report.CheckedFiles} 个文件）";
        }
        catch (Exception ex)
        {
            MaintenanceStatus = $"完整性校验失败：{ex.Message}";
        }
        finally
        {
            IsMaintenanceBusy = false;
        }
    }

    /// <summary>导出脱敏诊断包，返回 zip 路径。</summary>
    [RelayCommand]
    private async Task ExportDiagnosticsAsync()
    {
        if (IsMaintenanceBusy)
        {
            return;
        }

        IsMaintenanceBusy = true;
        MaintenanceStatus = "正在导出诊断包…";
        try
        {
            var path = await _facade.ExportDiagnosticsAsync(null, CancellationToken.None);
            MaintenanceStatus = $"诊断包已导出：{path}";
        }
        catch (Exception ex)
        {
            MaintenanceStatus = $"导出诊断包失败：{ex.Message}";
        }
        finally
        {
            IsMaintenanceBusy = false;
        }
    }

    /// <summary>检查更新；发现新版本后由「安装更新」按钮执行下载与校验。</summary>
    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        if (IsMaintenanceBusy)
        {
            return;
        }

        IsMaintenanceBusy = true;
        MaintenanceStatus = "正在检查更新…";
        try
        {
            PendingUpdate = await _facade.CheckUpdateAsync(CancellationToken.None);
            MaintenanceStatus = PendingUpdate is null
                ? "已是最新版本。"
                : $"发现新版本 {PendingUpdate.Version}，可点击「安装更新」。";
        }
        catch (Exception ex)
        {
            PendingUpdate = null;
            MaintenanceStatus = $"检查更新失败：{ex.Message}";
        }
        finally
        {
            IsMaintenanceBusy = false;
        }
    }

    /// <summary>下载并校验更新包，通过后启动安装器（E8001 时门面抛异常）。</summary>
    [RelayCommand]
    private async Task InstallUpdateAsync()
    {
        if (PendingUpdate is null || IsMaintenanceBusy)
        {
            return;
        }

        IsMaintenanceBusy = true;
        MaintenanceStatus = "正在下载更新…";
        var progress = new Progress<UpdateProgress>(p => MaintenanceStatus = p.Message);
        try
        {
            await _facade.ApplyUpdateAsync(PendingUpdate, progress, CancellationToken.None);
            MaintenanceStatus = "更新已下载并通过校验，安装器已启动。";
        }
        catch (Exception ex)
        {
            MaintenanceStatus = $"安装更新失败：{ex.Message}";
        }
        finally
        {
            IsMaintenanceBusy = false;
        }
    }

    /// <summary>切换深浅主题（M0 先直接改全局主题变体）。</summary>
    partial void OnIsDarkThemeChanged(bool value)
    {
        Application.Current!.RequestedThemeVariant = value
            ? Avalonia.Styling.ThemeVariant.Dark
            : Avalonia.Styling.ThemeVariant.Light;
    }
}
