using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Launcher.App.ViewModels;

namespace Launcher.App.Services;

/// <summary>
/// 当前启动会话的实时日志：首页负责写入，日志页读取展示。
/// </summary>
public sealed partial class RunLogStore : ObservableObject
{
    private const int HardLimit = 2200;
    private const int TrimCount = 200;

    public ObservableCollection<RunLogLine> Lines { get; } = [];

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string? fileName;

    [ObservableProperty]
    private string instanceName = string.Empty;

    [ObservableProperty]
    private bool hasContent;

    public void Begin(string instanceName, string? fileName)
    {
        InstanceName = instanceName;
        FileName = fileName;
        IsRunning = false;
        Lines.Clear();
        HasContent = false;
    }

    public void Append(RunLogLine line)
    {
        Lines.Add(line);
        if (!HasContent)
        {
            HasContent = true;
        }

        if (Lines.Count > HardLimit)
        {
            for (var i = 0; i < TrimCount; i++)
            {
                Lines.RemoveAt(0);
            }
        }
    }

    public void Complete() => IsRunning = false;
}
