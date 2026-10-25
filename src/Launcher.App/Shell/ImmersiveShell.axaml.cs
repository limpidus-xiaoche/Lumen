using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Launcher.App.ViewModels;
using Launcher.App.World;
using Launcher.Render;
using Serilog;

namespace Launcher.App.Shell;

public partial class ImmersiveShell : UserControl
{
    private readonly CameraController _camera;
    private readonly DispatcherTimer _cameraTick;
    private readonly InputRouter _router = new();
    private Window? _window;

    /// <summary>请求切换到经典视图，由宿主窗口决定窗口装饰与尺寸。</summary>
    public event EventHandler? ClassicViewRequested;

    private void OnClassicViewClick(object? sender, RoutedEventArgs e) =>
        ClassicViewRequested?.Invoke(this, EventArgs.Empty);

    public ImmersiveShell()
    {
        InitializeComponent();
        _camera = new CameraController(World.Camera);
        _cameraTick = new DispatcherTimer(DispatcherPriority.Render) { Interval = TimeSpan.FromMilliseconds(16) };
        _cameraTick.Tick += (_, _) => _camera.Update(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0);
        World.Degraded += OnDegraded;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window is not null)
        {
            InputRouter.Attach(_window, _router, () => _window.Close());
            _window.Activated += (_, _) => World.SetActive(true);
            _window.Deactivated += (_, _) =>
            {
                World.SetActive(false);
                _cameraTick.Stop();
            };
            _window.Opened += (_, _) => _cameraTick.Start();
        }

        PointerMoved += OnPointerMoved;
        _cameraTick.Start();
        _ = LoadPortalsAsync();
        _ = LoadAssetsAsync();
    }

    private async Task LoadAssetsAsync()
    {
        try
        {
            var assets = await Task.Run(MinecraftAssetProvider.TryLoad);
            if (assets is not null)
            {
                World.SetAssets(assets);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "加载方块资产失败，使用程序化配色");
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _cameraTick.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        var p = e.GetPosition(this);
        var w = Math.Max(1, Bounds.Width);
        var h = Math.Max(1, Bounds.Height);
        _camera.SetPointer((float)(p.X / w * 2 - 1), (float)(p.Y / h * 2 - 1));
    }

    private async Task LoadPortalsAsync()
    {
        if (DataContext is not MainWindowViewModel vm)
        {
            return;
        }

        try
        {
            var snapshot = await vm.Facade.GetSnapshotAsync(CancellationToken.None);
            var count = snapshot.Instances.Count;

            // 宣传/调试用：强制门数量
            var demo = Environment.GetEnvironmentVariable("LUMEN_DEMO_PORTALS");
            if (int.TryParse(demo, out var forced) && forced > 0)
            {
                count = forced;
            }

            if (count == 0)
            {
                count = 1; // 空态：一扇未成形的门
            }

            World.Portals = PortalLayout.Compute(count);
            Log.Information("世界层加载 {Count} 个实例，生成 {Portals} 扇门", snapshot.Instances.Count, World.Portals.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "读取实例列表失败，世界层只显示空态");
            World.Portals = [];
        }
    }

    private void OnDegraded(object? sender, string reason)
    {
        Dispatcher.UIThread.Post(() =>
        {
            World.IsVisible = false;
            DegradedBanner.IsVisible = true;
            DegradedText.Text = $"3D 世界层不可用，已切换为静态背景：{reason}";
        });
    }
}
