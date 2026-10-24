using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.OpenGL;
using Avalonia.OpenGL.Controls;
using Avalonia.Threading;
using Launcher.Render;
using Serilog;

namespace Launcher.App.World;

/// <summary>把 Avalonia 的 GlInterface 适配成渲染库需要的 IGlApi，使 Launcher.Render 不依赖 Avalonia。</summary>
internal sealed class GlApiAdapter(GlInterface gl) : IGlApi
{
    public nint GetProcAddress(string name) => gl.GetProcAddress(name);

    public string VersionString => gl.Version ?? string.Empty;

    public string RendererString => gl.Renderer ?? string.Empty;
}

/// <summary>世界层宿主。负责 GL 生命周期、60fps 节拍、失焦停表与降级。</summary>
public sealed class WorldView : OpenGlControlBase
{
    private readonly SceneRenderer _renderer = new();
    private readonly DispatcherTimer _tick;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private bool _ready;
    private double _lastFrameSeconds;
    private double _frameAccumulator;
    private int _frameCount;
    private bool _firstFrameLogged;

    private static readonly DateTime ProcessStartedAt = Process.GetCurrentProcess().StartTime;

    private static bool PerfLogEnabled =>
        string.Equals(Environment.GetEnvironmentVariable("LUMEN_PERF_LOG"), "1", StringComparison.Ordinal);

    public WorldView()
    {
        _tick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _tick.Tick += (_, _) =>
        {
            if (IsVisible)
            {
                RequestNextFrameRendering();
            }
        };
    }

    public Camera Camera { get; } = new();

    public IReadOnlyList<PortalSlot> Portals { get; set; } = [];

    /// <summary>由 App 后台加载的方块资产；GL 上下文就绪后由渲染器上传。</summary>
    public void SetAssets(SceneAssetSet? assets)
    {
        if (assets is not null)
        {
            Log.Information(
                "方块资产：岛 {Island} 组（顶点 {V}）/ 门框 {Frame} 组 / 贴图 {Tex}",
                assets.Island.Count,
                assets.Island.Sum(a => a.Mesh.VertexCount),
                assets.Frame.Count,
                assets.Textures.Count);
        }

        _renderer.SetAssets(assets);
    }

    /// <summary>GL 不可用时的原因；非 null 表示已降级。</summary>
    public string? DegradeReason { get; private set; }

    public event EventHandler<string>? Degraded;

    public static bool IsDisabledByEnvironment =>
        string.Equals(Environment.GetEnvironmentVariable("LUMEN_DISABLE_GL"), "1", StringComparison.Ordinal);

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (IsDisabledByEnvironment)
        {
            Degrade("LUMEN_DISABLE_GL=1，已强制降级");
            return;
        }

        _tick.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _tick.Stop();
        _renderer.Dispose();
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnOpenGlInit(GlInterface gl)
    {
        if (IsDisabledByEnvironment)
        {
            return;
        }

        var api = new GlApiAdapter(gl);
        Log.Information("GL 后端：{Version} / {Renderer}", api.VersionString, api.RendererString);

        if (!_renderer.Initialize(api))
        {
            Degrade(_renderer.FailureReason ?? "GL 初始化失败");
            return;
        }

        _ready = true;
        RequestNextFrameRendering();
    }

    protected override void OnOpenGlDeinit(GlInterface gl)
    {
        _renderer.Dispose();
        _ready = false;
    }

    protected override void OnOpenGlLost()
    {
        // 上下文丢失：资源句柄失效，交由下一次 OnOpenGlInit 重建。
        _renderer.Dispose();
        _ready = false;
        Log.Warning("GL 上下文丢失，等待重建");
    }

    protected override void OnOpenGlRender(GlInterface gl, int fb)
    {
        if (!_ready)
        {
            return;
        }

        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1.0;
        _renderer.Resize(
            (int)Math.Max(1, Bounds.Width * scaling),
            (int)Math.Max(1, Bounds.Height * scaling));

        _renderer.Render(_clock.Elapsed.TotalSeconds, Portals, Camera);

        if (!_firstFrameLogged)
        {
            _firstFrameLogged = true;
            var first = Portals.Count > 0 ? Portals[0] : default;
            Log.Information(
                "世界层首帧：{Elapsed:F0}ms | 布局 {W:F0}x{H:F0} scaling={S:F2} 像素 {PW}x{PH} | 相机 ({CX:F2},{CY:F2},{CZ:F2}) | 门数 {N} 首门 ({PX:F2},{PY:F2},{PZ:F2})",
                (DateTime.Now - ProcessStartedAt).TotalMilliseconds,
                Bounds.Width, Bounds.Height, scaling,
                (int)Math.Max(1, Bounds.Width * scaling), (int)Math.Max(1, Bounds.Height * scaling),
                Camera.Position.X, Camera.Position.Y, Camera.Position.Z,
                Portals.Count,
                first.Position.X, first.Position.Y, first.Position.Z);
        }

        if (PerfLogEnabled)
        {
            var now = _clock.Elapsed.TotalSeconds;
            if (_lastFrameSeconds > 0)
            {
                _frameAccumulator += now - _lastFrameSeconds;
                _frameCount++;
                if (_frameCount >= 120)
                {
                    var avgMs = _frameAccumulator / _frameCount * 1000.0;
                    Log.Information("帧时间：均值 {Avg:F2}ms（约 {Fps:F1}fps）", avgMs, 1000.0 / avgMs);
                    _frameAccumulator = 0;
                    _frameCount = 0;
                }
            }

            _lastFrameSeconds = now;
        }
    }

    /// <summary>窗口激活状态变化：失焦后降到 5fps，避免后台烧 CPU；重新激活恢复 60fps。</summary>
    public void SetActive(bool active)
    {
        _tick.Interval = active ? TimeSpan.FromMilliseconds(16) : TimeSpan.FromMilliseconds(200);
        if (active)
        {
            RequestNextFrameRendering();
        }
    }

    private void Degrade(string reason)
    {
        DegradeReason = reason;
        _tick.Stop();
        Log.Warning("世界层降级：{Reason}", reason);
        Degraded?.Invoke(this, reason);
    }
}
