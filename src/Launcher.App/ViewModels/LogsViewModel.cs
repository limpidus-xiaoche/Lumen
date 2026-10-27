using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Launcher.App.Services;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>日志页一行：启动器日志 + 每次启动落盘的游戏日志。</summary>
public sealed class LogEntryViewModel
{
    public LogEntryViewModel(string path, bool isGame, long size, DateTimeOffset time)
    {
        Path = path;
        IsGame = isGame;
        Size = size;
        Time = time;
    }

    public string Path { get; }

    public bool IsGame { get; }

    public long Size { get; }

    public DateTimeOffset Time { get; }

    public string FileName => System.IO.Path.GetFileName(Path);

    public string KindText => IsGame ? "游戏" : "启动器";

    public string SizeText => Size < 1024
        ? $"{Size} B"
        : Size < 1024 * 1024
            ? $"{Size / 1024.0:0.#} KB"
            : $"{Size / 1024.0 / 1024.0:0.##} MB";

    public string TimeText => Time.LocalDateTime.ToString("MM-dd HH:mm");
}

/// <summary>历史日志：启动器日志（Core 写入）+ 游戏输出（App 落盘到 logs/game）。</summary>
public partial class LogsViewModel : ObservableObject
{
    private const int PreviewLineLimit = 400;
    private const int MaxFiles = 80;

    private readonly ILauncherFacade _facade;
    private readonly string _logsDir;

    public ObservableCollection<LogEntryViewModel> Files { get; } = [];

    [ObservableProperty]
    private LogEntryViewModel? selectedFile;

    [ObservableProperty]
    private string previewText = "选择左侧日志查看最近 400 行内容";

    [ObservableProperty]
    private string previewTitle = "未选择日志";

    [ObservableProperty]
    private string statusText = "尚未加载";

    [ObservableProperty]
    private bool isBusy;

    /// <summary>当前启动会话的实时日志（首页写入）。</summary>
    public RunLogStore Live { get; }

    public LogsViewModel(ILauncherFacade facade, string logsDir, RunLogStore live)
    {
        _facade = facade;
        _logsDir = logsDir;
        Live = live;
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var entries = new List<LogEntryViewModel>();

            var launcherLogs = await _facade.GetLogFilesAsync(instanceId: null, CancellationToken.None);
            foreach (var log in launcherLogs)
            {
                entries.Add(new LogEntryViewModel(log.Path, isGame: false, log.Size, log.LastWriteAt));
            }

            entries.AddRange(EnumerateGameLogs());

            Files.Clear();
            foreach (var entry in entries
                         .OrderByDescending(e => e.Time)
                         .Take(MaxFiles))
            {
                Files.Add(entry);
            }

            var gameCount = Files.Count(f => f.IsGame);
            StatusText = Files.Count == 0
                ? "暂无日志：启动一次游戏后会自动落盘"
                : $"共 {Files.Count} 个日志（启动器 {Files.Count - gameCount} · 游戏 {gameCount}）";

            if (SelectedFile is null || !Files.Any(f => f.Path == SelectedFile.Path))
            {
                SelectedFile = Files.FirstOrDefault();
            }
        }
        catch (Exception ex)
        {
            StatusText = $"读取日志失败：{ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void OpenFolder()
    {
        try
        {
            Directory.CreateDirectory(_logsDir);
            Process.Start(new ProcessStartInfo("explorer.exe", $"\"{_logsDir}\"") { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            StatusText = $"打开日志目录失败：{ex.Message}";
        }
    }

    partial void OnSelectedFileChanged(LogEntryViewModel? value) => _ = LoadPreviewAsync(value);

    private async Task LoadPreviewAsync(LogEntryViewModel? entry)
    {
        if (entry is null)
        {
            PreviewTitle = "未选择日志";
            PreviewText = "选择左侧日志查看最近 400 行内容";
            return;
        }

        PreviewTitle = $"{entry.KindText} · {entry.FileName}";
        try
        {
            var text = await Task.Run(() =>
            {
                if (!File.Exists(entry.Path))
                {
                    return $"文件不存在：{entry.Path}";
                }

                var lines = File.ReadLines(entry.Path).TakeLast(PreviewLineLimit).ToArray();
                return lines.Length == 0 ? "（空文件）" : string.Join(Environment.NewLine, lines);
            });

            PreviewText = text;
        }
        catch (Exception ex)
        {
            PreviewText = $"读取失败：{ex.Message}";
        }
    }

    private IEnumerable<LogEntryViewModel> EnumerateGameLogs()
    {
        var gameDir = Path.Combine(_logsDir, "game");
        if (!Directory.Exists(gameDir))
        {
            return [];
        }

        return new DirectoryInfo(gameDir)
            .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
            .Select(f => new LogEntryViewModel(
                f.FullName,
                isGame: true,
                f.Length,
                new DateTimeOffset(f.LastWriteTimeUtc)));
    }
}
