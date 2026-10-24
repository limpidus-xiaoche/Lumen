using Launcher.Protocol;

namespace Launcher.Core.Install;

/// <summary>
/// 把安装进度限制到最多 10 次/秒，首帧与 Done 必发。
/// </summary>
public sealed class ThrottledProgress : IProgress<InstallProgress>
{
    private readonly IProgress<InstallProgress>? _inner;
    private readonly Action<InstallProgress>? _callback;
    private long _lastTick;

    public ThrottledProgress(IProgress<InstallProgress>? inner, Action<InstallProgress>? callback = null)
    {
        _inner = inner;
        _callback = callback;
    }

    public void Report(InstallProgress value)
    {
        var now = Environment.TickCount64;
        var force = value.Stage is InstallStage.Done || value.Percent <= 0;
        if (!force && now - _lastTick < 100)
        {
            return;
        }

        _lastTick = now;
        _inner?.Report(value);
        _callback?.Invoke(value);
    }
}
