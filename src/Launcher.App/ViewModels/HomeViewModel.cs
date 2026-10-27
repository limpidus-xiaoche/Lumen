using System.Collections.ObjectModel;
using System.Diagnostics;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>实时日志面板的一行。</summary>
public sealed record RunLogLine(string Text, IBrush Foreground, bool IsStderr)
{
    private static readonly IBrush SystemBrush = new SolidColorBrush(Color.Parse("#C9A7FF"));
    private static readonly IBrush StdoutBrush = new SolidColorBrush(Color.Parse("#C9C3D6"));
    private static readonly IBrush StderrBrush = new SolidColorBrush(Color.Parse("#FF9B93"));
    private static readonly IBrush ExitBrush = new SolidColorBrush(Color.Parse("#FFD28A"));

    public static RunLogLine System(string text) => new($"> {text}", SystemBrush, false);

    public static RunLogLine Exit(string text) => new($"> {text}", ExitBrush, false);

    public static RunLogLine Output(LaunchEventKind kind, string line) => kind == LaunchEventKind.Stderr
        ? new(line, StderrBrush, true)
        : new(line, StdoutBrush, false);
}

/// <summary>
/// 启动页：左列账号卡 + 皮肤模型，右列最近游戏 + 启动按钮。
/// 布局与预览稿一致；实时日志写入 <see cref="RunLogStore"/>，在日志页展示。
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private const string DefaultSkinFormat = "64×64 · 经典";

    private readonly ILauncherFacade _facade;
    private readonly LogsViewModel _logs;
    private readonly RunLogStore _runLog;
    private readonly InstancesViewModel _instances;
    private readonly string _logsDir;
    private readonly Action<object> _openSubPage;
    private readonly Action _goHome;
    private readonly IImage? _defaultSkin;
    private readonly IImage? _defaultCape;

    private GameLogRecorder? _recorder;

    public ObservableCollection<InstanceCardViewModel> Instances { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(VersionText))]
    [NotifyPropertyChangedFor(nameof(LaunchButtonText))]
    private InstanceCardViewModel? selectedInstance;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaunchButtonText))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LaunchButtonText))]
    private bool isLaunching;

    [ObservableProperty]
    private string statusText = "就绪";

    [ObservableProperty]
    private IImage? skinImage;

    [ObservableProperty]
    private string skinFormat = DefaultSkinFormat;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CapeStatusText))]
    private bool capeEnabled = true;

    /// <summary>当前实例的版本信息，如「1.21.1 · Fabric 0.16.10」。</summary>
    public string VersionText => SelectedInstance is null
        ? "未选择实例"
        : SelectedInstance.VersionLine;

    public string LaunchButtonText => IsLaunching ? "启动中…" : IsRunning ? "停止游戏" : "启动游戏";

    public string CapeStatusText => CapeEnabled ? "披风 · 已检测" : "披风 · 已隐藏";

    public IImage? CapeImage => _defaultCape;

    public HomeViewModel(
        ILauncherFacade facade,
        LogsViewModel logs,
        RunLogStore runLog,
        InstancesViewModel instances,
        string logsDir,
        Action<object> openSubPage,
        Action goHome)
    {
        _facade = facade;
        _logs = logs;
        _runLog = runLog;
        _instances = instances;
        _logsDir = logsDir;
        _openSubPage = openSubPage;
        _goHome = goHome;

        _defaultSkin = LoadAsset("skin-steve.png");
        _defaultCape = LoadAsset("cape-lumen.png");
        SkinImage = _defaultSkin;

        _ = LoadAsync();
    }

    /// <summary>刷新实例列表与运行会话状态（应用启动、手动刷新时调用）。</summary>
    public async Task LoadAsync()
    {
        try
        {
            var snapshot = await _facade.GetSnapshotAsync(CancellationToken.None);
            var sessions = await _facade.GetRunningSessionsAsync(CancellationToken.None);
            var running = sessions.Select(s => s.InstanceId).ToHashSet(StringComparer.Ordinal);

            var selectedId = SelectedInstance?.Id;
            Instances.Clear();
            var index = 0;
            foreach (var instance in snapshot.Instances
                         .OrderByDescending(i => i.LastPlayedAt ?? DateTimeOffset.MinValue)
                         .ThenBy(i => i.Name, StringComparer.CurrentCulture))
            {
                var card = new InstanceCardViewModel(instance)
                {
                    IsRunning = running.Contains(instance.Id),
                    IsRecent = index++ == 0,
                };
                Instances.Add(card);
            }

            SelectedInstance = Instances.FirstOrDefault(c => c.Id == selectedId)
                            ?? Instances.FirstOrDefault()
                            ?? null;

            var active = Instances.FirstOrDefault(c => c.IsRunning);
            IsRunning = active is not null && active.Id == SelectedInstance?.Id;
            StatusText = Instances.Count == 0
                ? "还没有实例：先到下载页安装一个版本"
                : active is not null
                    ? $"{active.Name} 正在运行"
                    : $"就绪 · {SelectedInstance?.Name}";
        }
        catch (Exception ex)
        {
            StatusText = $"读取实例失败：{ex.Message}";
        }
    }

    [RelayCommand]
    private async Task RefreshAsync() => await LoadAsync();

    /// <summary>大按钮：未运行则启动，运行中则优雅停止。</summary>
    [RelayCommand]
    private async Task ToggleLaunchAsync()
    {
        if (SelectedInstance is null)
        {
            StatusText = "没有可启动的实例";
            return;
        }

        if (IsRunning)
        {
            await StopAsync();
            return;
        }

        await LaunchAsync();
    }

    private async Task LaunchAsync()
    {
        var card = SelectedInstance!;
        if (!card.CanLaunch)
        {
            StatusText = card.IsReady ? "该实例正在启动或运行中" : "实例尚未就绪，请先修复";
            return;
        }

        IsLaunching = true;
        StatusText = $"正在启动 {card.Name}…";

        _recorder = GameLogRecorder.TryCreate(_logsDir, card.Name);
        _runLog.Begin(card.Name, _recorder?.FilePath is { } path ? Path.GetFileName(path) : null);
        Append(RunLogLine.System($"启动 {card.Name}（{card.VersionLine}）"));

        try
        {
            var progress = new Progress<LaunchEvent>(e => UiThread.Post(() => OnLaunchEvent(card, e)));
            var session = await _facade.LaunchAsync(card.Id, progress, CancellationToken.None);

            card.IsRunning = true;
            IsRunning = true;
            _runLog.IsRunning = true;
            StatusText = $"游戏运行中（PID {session.ProcessId}）";
            Append(RunLogLine.System($"游戏进程已启动（PID {session.ProcessId}）"));
            Persist("system", $"游戏进程已启动（PID {session.ProcessId}）");
        }
        catch (Exception ex)
        {
            StatusText = $"启动失败：{ex.Message}";
            Append(RunLogLine.System($"启动失败：{ex.Message}"));
            Persist("error", ex.Message);
            CloseRecorder();
        }
        finally
        {
            IsLaunching = false;
        }
    }

    private async Task StopAsync()
    {
        try
        {
            Append(RunLogLine.System("正在请求停止游戏…"));
            await _facade.StopGameAsync(SelectedInstance!.Id, StopMode.Graceful, CancellationToken.None);
            StatusText = "已请求停止游戏（等待进程退出）";
        }
        catch (Exception ex)
        {
            StatusText = $"停止失败：{ex.Message}";
            Append(RunLogLine.System($"停止失败：{ex.Message}"));
        }
    }

    private void OnLaunchEvent(InstanceCardViewModel card, LaunchEvent e)
    {
        switch (e.Kind)
        {
            case LaunchEventKind.Started:
                Append(RunLogLine.System(e.Line));
                Persist("system", e.Line);
                break;

            case LaunchEventKind.Stdout:
                Append(RunLogLine.Output(e.Kind, e.Line));
                Persist("out", e.Line);
                break;

            case LaunchEventKind.Stderr:
                Append(RunLogLine.Output(e.Kind, e.Line));
                Persist("err", e.Line);
                break;

            case LaunchEventKind.Crashed:
                Append(RunLogLine.Exit(e.Line));
                Persist("crash", e.Line);
                break;

            case LaunchEventKind.Exited:
                var hint = string.IsNullOrWhiteSpace(e.CrashHint) ? string.Empty : $" · {e.CrashHint}";
                Persist("exit", $"{e.Line}{hint}");
                CloseRecorder();
                card.IsRunning = false;
                IsRunning = false;
                _runLog.Complete();
                StatusText = $"游戏已退出（代码 {e.ExitCode}）{hint}";
                Append(RunLogLine.Exit($"{e.Line}{hint}"));
                break;
        }
    }

    private void Append(RunLogLine line) => _runLog.Append(line);

    private void Persist(string stream, string line) => _recorder?.Write(stream, line);

    private void CloseRecorder()
    {
        _recorder?.Dispose();
        _recorder = null;
    }

    /// <summary>打开全部实例页。</summary>
    [RelayCommand]
    private void OpenAllInstances()
    {
        _ = _instances.LoadAsync();
        _openSubPage(_instances);
    }

    /// <summary>打开历史日志页（含本次启动落盘的日志）。</summary>
    [RelayCommand]
    private void OpenHistoryLogs()
    {
        _logs.LoadCommand.Execute(null);
        _openSubPage(_logs);
    }

    /// <summary>在资源管理器中打开当前实例目录。</summary>
    [RelayCommand]
    private async Task OpenGameFolderAsync()
    {
        if (SelectedInstance is null)
        {
            return;
        }

        try
        {
            var detail = await _facade.GetInstanceAsync(SelectedInstance.Id, CancellationToken.None);
            if (!Directory.Exists(detail.GameDir))
            {
                StatusText = $"目录不存在：{detail.GameDir}";
                return;
            }

            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{detail.GameDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText = $"打开目录失败：{ex.Message}";
        }
    }

    /// <summary>打开当前实例的版本设置页。</summary>
    [RelayCommand]
    private void OpenVersionSettings()
    {
        if (SelectedInstance is null)
        {
            StatusText = "没有可设置的实例";
            return;
        }

        _openSubPage(new VersionSettingsViewModel(_facade, SelectedInstance.Summary, _goHome));
    }

    /// <summary>导入 PNG 皮肤，自动识别 64×64 / 64×32。</summary>
    public void SetSkin(Bitmap bitmap)
    {
        var size = bitmap.PixelSize;
        if (size.Width != 64 || (size.Height != 64 && size.Height != 32))
        {
            StatusText = "皮肤需为 64×64 或 64×32 的 PNG";
            return;
        }

        SkinImage = bitmap;
        SkinFormat = $"{size.Width}×{size.Height} · 经典";
        StatusText = "皮肤已导入（仅本次会话）";
    }

    [RelayCommand]
    private void ResetSkin()
    {
        SkinImage = _defaultSkin;
        SkinFormat = DefaultSkinFormat;
        StatusText = "已恢复默认皮肤";
    }

    partial void OnSelectedInstanceChanged(InstanceCardViewModel? value)
    {
        IsRunning = value?.IsRunning ?? false;
        if (!IsLaunching)
        {
            StatusText = IsRunning ? $"{value?.Name} 正在运行" : $"就绪 · {value?.Name}";
        }
    }

    private static IImage? LoadAsset(string fileName)
    {
        try
        {
            return new Bitmap(AssetLoader.Open(new Uri($"avares://Lumen/Assets/{fileName}")));
        }
        catch
        {
            // 单元测试等无 Avalonia 资源宿主的场景：没有默认皮肤也能正常工作。
            return null;
        }
    }
}
