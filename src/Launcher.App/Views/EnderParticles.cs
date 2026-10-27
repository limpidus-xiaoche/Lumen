using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Launcher.App.Views;

/// <summary>末影粒子：缓慢上浮的暗紫微粒，纯装饰、不参与命中测试。</summary>
public sealed class EnderParticles : Control
{
    private readonly DispatcherTimer _timer;
    private readonly List<Particle> _particles = [];
    private readonly Random _random = new(20261024);
    private readonly SolidColorBrush _core = new(Color.Parse("#C9A7FF"));
    private readonly SolidColorBrush _halo = new(Color.Parse("#A275F5"));
    private readonly SolidColorBrush _dark = new(Color.Parse("#3D2A5C"));
    private double _time;

    public EnderParticles()
    {
        IsHitTestVisible = false;
        ClipToBounds = true;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _timer.Tick += (_, _) => Step();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_particles.Count == 0 && finalSize.Width > 0 && finalSize.Height > 0)
        {
            Spawn(finalSize);
        }

        return base.ArrangeOverride(finalSize);
    }

    private void Spawn(Size size)
    {
        for (var i = 0; i < 16; i++)
        {
            _particles.Add(new Particle
            {
                X = _random.NextDouble() * size.Width,
                Y = _random.NextDouble() * size.Height,
                Speed = 4 + _random.NextDouble() * 9,
                Radius = 1.2 + _random.NextDouble() * 1.6,
                Alpha = 0.06 + _random.NextDouble() * 0.24,
                Phase = _random.NextDouble() * Math.PI * 2,
                IsDark = _random.Next(3) == 0,
            });
        }
    }

    private void Step()
    {
        if (Bounds.Width <= 0 || Bounds.Height <= 0)
        {
            return;
        }

        _time += 0.05;
        foreach (var particle in _particles)
        {
            particle.Y -= particle.Speed * 0.05;
            particle.X += Math.Sin(_time * 0.6 + particle.Phase) * 0.18;

            if (particle.Y < -6)
            {
                particle.Y = Bounds.Height + _random.NextDouble() * 20;
                particle.X = _random.NextDouble() * Bounds.Width;
            }
        }

        InvalidateVisual();
    }

    public override void Render(DrawingContext context)
    {
        foreach (var particle in _particles)
        {
            using (context.PushOpacity(particle.Alpha * 0.6))
            {
                var halo = particle.Radius * 2.6;
                context.DrawRectangle(_halo, null, new Rect(particle.X - halo / 2, particle.Y - halo / 2, halo, halo));
            }

            using (context.PushOpacity(particle.Alpha))
            {
                var brush = particle.IsDark ? _dark : _core;
                context.DrawRectangle(brush, null, new Rect(particle.X, particle.Y, particle.Radius, particle.Radius));
            }
        }
    }

    private sealed class Particle
    {
        public double X { get; set; }
        public double Y { get; set; }
        public double Speed { get; init; }
        public double Radius { get; init; }
        public double Alpha { get; init; }
        public double Phase { get; init; }
        public bool IsDark { get; init; }
    }
}
