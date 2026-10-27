using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Launcher.App.Views;

/// <summary>
/// 轻量 3D 皮肤预览：正交投影的立方体玩家模型，支持拖拽旋转、滚轮缩放、原地行走与披风。
/// 视觉对齐预览稿的 skinview3d 舞台（不自动旋转、不挥手）。
/// </summary>
public class SkinPreview : Control
{
    public static readonly StyledProperty<IImage?> SkinProperty =
        AvaloniaProperty.Register<SkinPreview, IImage?>(nameof(Skin));

    public static readonly StyledProperty<IImage?> CapeProperty =
        AvaloniaProperty.Register<SkinPreview, IImage?>(nameof(Cape));

    public static readonly StyledProperty<bool> ShowCapeProperty =
        AvaloniaProperty.Register<SkinPreview, bool>(nameof(ShowCape), defaultValue: true);

    private const double Pitch = -0.14;

    private readonly List<Part> _parts = [];
    private readonly List<Face> _looseFaces = [];
    private readonly DispatcherTimer _timer;
    private readonly Stopwatch _clock = new();

    private double _yaw = 0.85;
    private double _zoom = 1.0;
    private double _phase;
    private Point? _lastPointer;

    public SkinPreview()
    {
        ClipToBounds = true;
        Cursor = new Cursor(StandardCursorType.Hand);
        RenderOptions.SetBitmapInterpolationMode(this, BitmapInterpolationMode.None);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += (_, _) => Tick();
    }

    static SkinPreview()
    {
        AffectsRender<SkinPreview>(SkinProperty, CapeProperty, ShowCapeProperty);
    }

    public IImage? Skin
    {
        get => GetValue(SkinProperty);
        set => SetValue(SkinProperty, value);
    }

    public IImage? Cape
    {
        get => GetValue(CapeProperty);
        set => SetValue(CapeProperty, value);
    }

    public bool ShowCape
    {
        get => GetValue(ShowCapeProperty);
        set => SetValue(ShowCapeProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SkinProperty || change.Property == CapeProperty || change.Property == ShowCapeProperty)
        {
            Rebuild();
            InvalidateVisual();
        }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (Skin is null)
        {
            Rebuild();
        }

        _clock.Restart();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
        _clock.Stop();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        _lastPointer = e.GetPosition(this);
        e.Pointer.Capture(this);
        Cursor = new Cursor(StandardCursorType.SizeAll);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (_lastPointer is not { } last)
        {
            return;
        }

        var now = e.GetPosition(this);
        _yaw += (now.X - last.X) * 0.012;
        _lastPointer = now;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _lastPointer = null;
        e.Pointer.Capture(null);
        Cursor = new Cursor(StandardCursorType.Hand);
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        _zoom = Math.Clamp(_zoom * (1 + e.Delta.Y * 0.08), 0.6, 1.8);
        InvalidateVisual();
        e.Handled = true;
    }

    private void Tick()
    {
        var dt = _clock.Elapsed.TotalSeconds;
        _clock.Restart();
        if (dt <= 0 || dt > 0.1)
        {
            dt = 0.016;
        }

        _phase += dt * 6.0;
        if (_phase > Math.PI * 200)
        {
            _phase -= Math.PI * 200;
        }

        if (IsVisible)
        {
            InvalidateVisual();
        }
    }

    public override void Render(DrawingContext context)
    {
        base.Render(context);
        if (_parts.Count == 0 && _looseFaces.Count == 0)
        {
            return;
        }

        var scale = Math.Min(Bounds.Width / 42.0, Bounds.Height / 46.0) * 1.2 * _zoom;
        var center = new Point(Bounds.Width / 2, Bounds.Height / 2 + 16 * scale);

        var renders = new List<RenderFace>();
        foreach (var part in _parts)
        {
            var angle = part.Swing * Math.Sin(_phase) * 0.55;
            foreach (var face in part.Faces)
            {
                AddRender(renders, face, part, angle, scale, center);
            }
        }

        foreach (var face in _looseFaces)
        {
            AddRender(renders, face, part: null, angle: 0, scale, center);
        }

        renders.Sort((a, b) => a.Depth.CompareTo(b.Depth));
        foreach (var item in renders)
        {
            DrawFace(context, item);
        }
    }

    private void AddRender(
        List<RenderFace> target,
        Face face,
        Part? part,
        double angle,
        double scale,
        Point center)
    {
        var a = part is null ? face.A : Rotate(face.A, part, angle);
        var b = part is null ? face.B : Rotate(face.B, part, angle);
        var c = part is null ? face.C : Rotate(face.C, part, angle);
        if (part?.MirrorTexture == true)
        {
            (a, b) = (b, a);
        }

        var d = new Vec3(b.X + c.X - a.X, b.Y + c.Y - a.Y, b.Z + c.Z - a.Z);

        var pa = Project(a, scale, center);
        var pb = Project(b, scale, center);
        var pc = Project(c, scale, center);
        var pd = Project(d, scale, center);

        if (!IsQuadVisible(pa, pb, pc, pd))
        {
            return;
        }

        target.Add(new RenderFace
        {
            Texture = face.Texture,
            Width = face.Width,
            Height = face.Height,
            Brightness = face.Brightness,
            A = pa.Point,
            B = pb.Point,
            C = pc.Point,
            D = pd.Point,
            Depth = (pa.Depth + pb.Depth + pc.Depth + pd.Depth) / 4,
        });
    }

    private static bool IsQuadVisible(Projected a, Projected b, Projected c, Projected d)
    {
        var area = (b.Point.X - a.Point.X) * (c.Point.Y - a.Point.Y)
                   - (c.Point.X - a.Point.X) * (b.Point.Y - a.Point.Y);
        return Math.Abs(area) > 0.5 && !double.IsNaN(area) && !double.IsInfinity(area)
               && !double.IsNaN(d.Point.X) && !double.IsNaN(d.Point.Y);
    }

    private static void DrawFace(DrawingContext context, RenderFace item)
    {
        var ux = (item.B.X - item.A.X) / item.Width;
        var uy = (item.B.Y - item.A.Y) / item.Width;
        var vx = (item.C.X - item.A.X) / item.Height;
        var vy = (item.C.Y - item.A.Y) / item.Height;

        var matrix = new Matrix(ux, uy, vx, vy, item.A.X, item.A.Y);
        using (context.PushTransform(matrix))
        {
            context.DrawImage(item.Texture, new Rect(0, 0, item.Width, item.Height));
        }

        var shade = item.Brightness;
        if (shade < 0.999)
        {
            var alpha = (byte)Math.Clamp((1 - shade) * 220, 0, 110);
            context.DrawGeometry(new SolidColorBrush(Color.FromArgb(alpha, 0, 0, 0)), null, Quad(item));
        }
        else if (shade > 1.001)
        {
            var alpha = (byte)Math.Clamp((shade - 1) * 200, 0, 45);
            context.DrawGeometry(new SolidColorBrush(Color.FromArgb(alpha, 255, 255, 255)), null, Quad(item));
        }
    }

    private static StreamGeometry Quad(RenderFace item)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        ctx.BeginFigure(item.A, isFilled: true);
        ctx.LineTo(item.B);
        ctx.LineTo(item.D);
        ctx.LineTo(item.C);
        ctx.EndFigure(isClosed: true);
        return geometry;
    }

    private static Vec3 Rotate(Vec3 p, Part part, double angle)
    {
        var dy = p.Y - part.PivotY;
        var dz = p.Z - part.PivotZ;
        var cos = Math.Cos(angle);
        var sin = Math.Sin(angle);
        return new Vec3(
            p.X,
            part.PivotY + dy * cos - dz * sin,
            part.PivotZ + dy * sin + dz * cos);
    }

    private Projected Project(Vec3 p, double scale, Point center)
    {
        var cosY = Math.Cos(_yaw);
        var sinY = Math.Sin(_yaw);
        var x1 = p.X * cosY + p.Z * sinY;
        var z1 = -p.X * sinY + p.Z * cosY;
        var cosP = Math.Cos(Pitch);
        var sinP = Math.Sin(Pitch);
        var y2 = p.Y * cosP - z1 * sinP;
        var z2 = p.Y * sinP + z1 * cosP;

        return new Projected(new Point(center.X + x1 * scale, center.Y - y2 * scale), z2);
    }

    private void Rebuild()
    {
        _parts.Clear();
        _looseFaces.Clear();

        if (Skin is not { } skin)
        {
            return;
        }

        var pixels = PixelSizeOf(skin);
        if (pixels.Width < 64 || pixels.Height < 32)
        {
            return;
        }

        var legacy = pixels.Height < 64;

        CroppedBitmap? Crop(int x, int y, int w, int h)
        {
            if (x < 0 || y < 0 || x + w > pixels.Width || y + h > pixels.Height)
            {
                return null;
            }

            return new CroppedBitmap(skin, new PixelRect(x, y, w, h));
        }

        // 头（含帽子层）
        var head = NewPart(pivotY: 24);
        AddBox(head, Crop, 0, 0, 8, 8, 8, -4, 4, 24, 32, -4, 4, mirrorTexture: false);
        AddBox(head, Crop, 32, 0, 8, 8, 8, -4.4, 4.4, 23.6, 32.4, -4.4, 4.4, mirrorTexture: false, shade: 1.04);

        // 身体 + 夹克
        var body = NewPart(pivotY: 12);
        AddBox(body, Crop, 16, 16, 8, 12, 4, -4, 4, 12, 24, -2, 2, mirrorTexture: false);
        if (!legacy)
        {
            AddBox(body, Crop, 16, 32, 8, 12, 4, -4.35, 4.35, 11.65, 24.35, -2.35, 2.35, mirrorTexture: false);
        }

        // 手臂（64×32 时左臂镜像右臂贴图）
        var rightArm = NewPart(pivotY: 24, swing: -1);
        AddBox(rightArm, Crop, 40, 16, 4, 12, 4, -8, -4, 12, 24, -2, 2, mirrorTexture: false);
        if (!legacy)
        {
            AddBox(rightArm, Crop, 40, 32, 4, 12, 4, -8.35, -3.65, 11.65, 24.35, -2.35, 2.35, mirrorTexture: false);
        }

        var leftArm = NewPart(pivotY: 24, swing: 1);
        AddBox(leftArm, Crop, legacy ? 40 : 32, legacy ? 16 : 48, 4, 12, 4, 4, 8, 12, 24, -2, 2, mirrorTexture: legacy);
        if (!legacy)
        {
            AddBox(leftArm, Crop, 48, 48, 4, 12, 4, 3.65, 8.35, 11.65, 24.35, -2.35, 2.35, mirrorTexture: false);
        }

        // 腿（64×32 时左腿镜像右腿贴图）
        var rightLeg = NewPart(pivotY: 12, swing: 1);
        AddBox(rightLeg, Crop, 0, 16, 4, 12, 4, -4, 0, 0, 12, -2, 2, mirrorTexture: false);
        if (!legacy)
        {
            AddBox(rightLeg, Crop, 0, 32, 4, 12, 4, -4.35, 0.35, -0.35, 12.35, -2.35, 2.35, mirrorTexture: false);
        }

        var leftLeg = NewPart(pivotY: 12, swing: -1);
        AddBox(leftLeg, Crop, legacy ? 0 : 16, legacy ? 16 : 48, 4, 12, 4, 0, 4, 0, 12, -2, 2, mirrorTexture: legacy);
        if (!legacy)
        {
            AddBox(leftLeg, Crop, 0, 48, 4, 12, 4, -0.35, 4.35, -0.35, 12.35, -2.35, 2.35, mirrorTexture: false);
        }

        // 披风
        if (ShowCape && Cape is { } cape && PixelSizeOf(cape).Width >= 22 && PixelSizeOf(cape).Height >= 17)
        {
            var outer = CropCape(cape, 12, 1, 10, 16);
            var inner = CropCape(cape, 1, 1, 10, 16);
            if (outer is not null)
            {
                _looseFaces.Add(new Face
                {
                    A = new Vec3(5, 24, -2.6),
                    B = new Vec3(-5, 24, -2.6),
                    C = new Vec3(5, 8, -2.6),
                    Texture = outer,
                    Width = 10,
                    Height = 16,
                    Brightness = 0.95,
                });
            }

            if (inner is not null)
            {
                _looseFaces.Add(new Face
                {
                    A = new Vec3(-5, 24, -2.2),
                    B = new Vec3(5, 24, -2.2),
                    C = new Vec3(-5, 8, -2.2),
                    Texture = inner,
                    Width = 10,
                    Height = 16,
                    Brightness = 0.7,
                });
            }
        }

    }

    private static CroppedBitmap? CropCape(IImage cape, int x, int y, int w, int h)
    {
        var pixels = PixelSizeOf(cape);
        if (x + w > pixels.Width || y + h > pixels.Height)
        {
            return null;
        }

        return new CroppedBitmap(cape, new PixelRect(x, y, w, h));
    }

    private static PixelSize PixelSizeOf(IImage image) => image is Bitmap bitmap
        ? bitmap.PixelSize
        : new PixelSize((int)Math.Round(image.Size.Width), (int)Math.Round(image.Size.Height));

    private Part NewPart(double pivotY, double swing = 0)
    {
        var part = new Part { PivotY = pivotY, Swing = swing };
        _parts.Add(part);
        return part;
    }

    private static void AddBox(
        Part part,
        Func<int, int, int, int, CroppedBitmap?> crop,
        int u,
        int v,
        int w,
        int h,
        int d,
        double minX,
        double maxX,
        double minY,
        double maxY,
        double minZ,
        double maxZ,
        bool mirrorTexture,
        double shade = 1)
    {
        part.MirrorTexture = mirrorTexture;

        void Add(CroppedBitmap? texture, int texWidth, int texHeight, Vec3 a, Vec3 b, Vec3 c, double brightness)
        {
            if (texture is null)
            {
                return;
            }

            part.Faces.Add(new Face
            {
                A = a,
                B = b,
                C = c,
                Texture = texture,
                Width = texWidth,
                Height = texHeight,
                Brightness = brightness * shade,
            });
        }

        // 右面 (+X)
        Add(crop(u, v + d, d, h), d, h,
            new Vec3(maxX, maxY, maxZ), new Vec3(maxX, maxY, minZ), new Vec3(maxX, minY, maxZ), 0.9);
        // 前面 (+Z)
        Add(crop(u + d, v + d, w, h), w, h,
            new Vec3(minX, maxY, maxZ), new Vec3(maxX, maxY, maxZ), new Vec3(minX, minY, maxZ), 1.0);
        // 左面 (-X)
        Add(crop(u + d + w, v + d, d, h), d, h,
            new Vec3(minX, maxY, minZ), new Vec3(minX, maxY, maxZ), new Vec3(minX, minY, minZ), 0.78);
        // 后面 (-Z)
        Add(crop(u + d + w + d, v + d, w, h), w, h,
            new Vec3(maxX, maxY, minZ), new Vec3(minX, maxY, minZ), new Vec3(maxX, minY, minZ), 0.86);
        // 顶面 (+Y)
        Add(crop(u + d, v, w, d), w, d,
            new Vec3(minX, maxY, minZ), new Vec3(maxX, maxY, minZ), new Vec3(minX, maxY, maxZ), 1.16);
        // 底面 (-Y)
        Add(crop(u + d + w, v, w, d), w, d,
            new Vec3(minX, minY, maxZ), new Vec3(maxX, minY, maxZ), new Vec3(minX, minY, minZ), 0.62);
    }

    private readonly record struct Vec3(double X, double Y, double Z);

    private readonly record struct Projected(Point Point, double Depth);

    private sealed class Part
    {
        public double PivotY;
        public double PivotZ { get; set; }
        public double Swing;
        public bool MirrorTexture;
        public List<Face> Faces { get; } = [];
    }

    private sealed class Face
    {
        public Vec3 A;
        public Vec3 B;
        public Vec3 C;
        public IImage Texture = null!;
        public double Width;
        public double Height;
        public double Brightness;
    }

    private sealed class RenderFace
    {
        public IImage Texture = null!;
        public double Width;
        public double Height;
        public double Brightness;
        public Point A;
        public Point B;
        public Point C;
        public Point D;
        public double Depth;
    }
}
