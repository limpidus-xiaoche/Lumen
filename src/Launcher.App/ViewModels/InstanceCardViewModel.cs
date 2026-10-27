using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.Protocol;

namespace Launcher.App.ViewModels;

/// <summary>实例展示用的文本格式，首页与实例页共用。</summary>
public static class InstanceDisplay
{
    public static string LoaderLabel(LoaderKind loader) => loader switch
    {
        LoaderKind.Vanilla => "原版",
        LoaderKind.Fabric => "Fabric",
        LoaderKind.Forge => "Forge",
        LoaderKind.NeoForge => "NeoForge",
        _ => loader.ToString(),
    };

    public static string VersionLine(InstanceSummary instance) =>
        $"{instance.VersionId} · {LoaderLabel(instance.Loader)}" +
        (string.IsNullOrWhiteSpace(instance.LoaderVersion) ? string.Empty : $" {instance.LoaderVersion}");

    public static string StateLabel(InstanceState state) => state switch
    {
        InstanceState.Installing => "安装中",
        InstanceState.Ready => "就绪",
        InstanceState.Broken => "损坏",
        InstanceState.Repairing => "修复中",
        _ => state.ToString(),
    };
}

/// <summary>实例卡片：首页「最近游戏」和实例页共用一份展示状态。</summary>
public partial class InstanceCardViewModel : ObservableObject
{
    public InstanceCardViewModel(InstanceSummary summary)
    {
        Summary = summary;
    }

    public InstanceSummary Summary { get; }

    public string Id => Summary.Id;

    public string Name => Summary.Name;

    public string Initial => string.IsNullOrEmpty(Summary.Name) ? "?" : Summary.Name[..1];

    public string VersionLine => InstanceDisplay.VersionLine(Summary);

    public string Title => Summary.Loader == LoaderKind.Vanilla
        ? Summary.VersionId
        : $"{Summary.VersionId} {InstanceDisplay.LoaderLabel(Summary.Loader)}";

    public string Subtitle =>
        $"{InstanceDisplay.LoaderLabel(Summary.Loader)} · {LastPlayedRelative}";

    public string StateText => InstanceDisplay.StateLabel(Summary.State);

    public bool IsReady => Summary.State == InstanceState.Ready;

    public bool IsBroken => Summary.State == InstanceState.Broken;

    public string LastPlayedText => Summary.LastPlayedAt is { } played
        ? $"最近游玩 {played.LocalDateTime:MM-dd HH:mm}"
        : "尚未启动";

    public string LastPlayedRelative
    {
        get
        {
            if (Summary.LastPlayedAt is not { } played)
            {
                return "尚未启动";
            }

            var span = DateTimeOffset.Now - played;
            return span.TotalMinutes switch
            {
                < 1 => "刚刚",
                < 60 => $"{(int)span.TotalMinutes} 分钟前",
                < 60 * 24 => $"{(int)span.TotalHours} 小时前",
                < 60 * 48 => "昨天",
                _ => played.LocalDateTime.ToString("MM-dd"),
            };
        }
    }

    /// <summary>是否标记为「最近」实例（列表中第一条）。</summary>
    [ObservableProperty]
    private bool isRecent;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanLaunch))]
    [NotifyPropertyChangedFor(nameof(CanStop))]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool isRunning;

    /// <summary>启动/修复时展示的最新一条消息。</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActivity))]
    private string? activityText;

    public bool HasActivity => !string.IsNullOrWhiteSpace(ActivityText);

    public bool CanLaunch => IsReady && !IsRunning;

    public bool CanStop => IsRunning;

    public bool CanRepair => !IsReady && !IsRunning;

    public string StatusText => IsRunning ? "运行中" : StateText;
}
