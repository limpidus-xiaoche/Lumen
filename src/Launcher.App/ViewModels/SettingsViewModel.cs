using System.Collections.ObjectModel;
using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.Core.Download;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    public const string DownloadSourceAutoText = "自动（官方优先，失败回退镜像）";
    public const string DownloadSourceOfficialText = "仅官方";
    public const string DownloadSourceMirrorText = "仅镜像";
    public const string ProxyModeSystemText = "系统代理";
    public const string ProxyModeManualText = "手动";
    public const string ProxyModeNoneText = "不使用代理";

    private readonly ILauncherFacade _facade;
    private readonly InstancesViewModel _instances;
    private readonly LogsViewModel _logs;
    private readonly Action<object> _openSubPage;

    public ObservableCollection<JavaRuntime> Javas { get; } = [];

    /// <summary>下载源选项（显示文案与枚举一一对应）。</summary>
    public ObservableCollection<string> DownloadSources { get; } =
        [DownloadSourceAutoText, DownloadSourceOfficialText, DownloadSourceMirrorText];

    /// <summary>代理模式选项（显示文案与枚举一一对应）。</summary>
    public ObservableCollection<string> ProxyModes { get; } =
        [ProxyModeSystemText, ProxyModeManualText, ProxyModeNoneText];

    [ObservableProperty]
    private string selectedDownloadSource = DownloadSourceAutoText;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsManualProxy))]
    private string selectedProxyMode = ProxyModeSystemText;

    [ObservableProperty]
    private string proxyAddress = string.Empty;

    /// <summary>手动代理地址仅在「手动」模式可编辑。</summary>
    public bool IsManualProxy => SelectedProxyMode == ProxyModeManualText;

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
        _ = LoadSettingsAsync();
    }

    /// <summary>读取当前设置并回填网络与下载控件。</summary>
    [RelayCommand]
    private async Task LoadSettingsAsync()
    {
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            SelectedDownloadSource = DownloadSourceText(snapshot.Settings.DownloadSource);
            SelectedProxyMode = ProxyModeText(snapshot.Settings.ProxyMode);
            ProxyAddress = snapshot.Settings.ProxyAddress ?? string.Empty;
        }
        catch (Exception ex)
        {
            StatusText = $"读取设置失败：{ex.Message}";
        }
    }

    /// <summary>保存内存、下载源与代理设置；代理应用结果一并写入状态栏。</summary>
    [RelayCommand]
    private async Task SaveAsync()
    {
        var patch = new AppSettingsPatch(
            MaxMemoryMb: MaxMemoryMb,
            DownloadSource: ParseDownloadSource(SelectedDownloadSource),
            ProxyMode: ParseProxyMode(SelectedProxyMode),
            ProxyAddress: string.IsNullOrWhiteSpace(ProxyAddress) ? null : ProxyAddress.Trim());
        await _facade.UpdateSettingsAsync(patch, CancellationToken.None);

        var applied = true;
        string? proxyError = null;
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            applied = NetworkPolicy.TryApply(snapshot.Settings, out proxyError);
        }
        catch (Exception ex)
        {
            applied = false;
            proxyError = ex.Message;
        }

        var proxyResult = applied
            ? $"代理 {SelectedProxyMode} 已应用"
            : $"代理未生效：{proxyError}";
        StatusText = $"已保存：最大内存 {MaxMemoryMb} MB，下载源 {SelectedDownloadSource}，{proxyResult}";
    }

    private static string DownloadSourceText(DownloadSourceMode mode) => mode switch
    {
        DownloadSourceMode.Official => DownloadSourceOfficialText,
        DownloadSourceMode.Mirror => DownloadSourceMirrorText,
        _ => DownloadSourceAutoText,
    };

    private static DownloadSourceMode ParseDownloadSource(string text) => text switch
    {
        DownloadSourceOfficialText => DownloadSourceMode.Official,
        DownloadSourceMirrorText => DownloadSourceMode.Mirror,
        _ => DownloadSourceMode.Auto,
    };

    private static string ProxyModeText(ProxyMode mode) => mode switch
    {
        ProxyMode.Manual => ProxyModeManualText,
        ProxyMode.None => ProxyModeNoneText,
        _ => ProxyModeSystemText,
    };

    private static ProxyMode ParseProxyMode(string text) => text switch
    {
        ProxyModeManualText => ProxyMode.Manual,
        ProxyModeNoneText => ProxyMode.None,
        _ => ProxyMode.System,
    };

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
