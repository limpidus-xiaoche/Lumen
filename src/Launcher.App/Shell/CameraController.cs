using Launcher.Render;

namespace Launcher.App.Shell;

/// <summary>镜头：8 秒一圈的缓慢公转 + 鼠标视差，全部限制在小幅度内。</summary>
public sealed class CameraController
{
    private const float OrbitAmplitudeRadians = 3f * MathF.PI / 180f;
    private const float ParallaxAmplitudeRadians = 2f * MathF.PI / 180f;
    private const float OrbitPeriodSeconds = 8f;
    private const float Distance = 12.6f;
    private const float EyeHeight = 3.15f;

    private float _pointerX;
    private float _pointerY;

    public CameraController(Camera camera)
    {
        Camera = camera;
    }

    public Camera Camera { get; }

    /// <summary>指针归一化坐标，范围 [-1, 1]。</summary>
    public void SetPointer(float x, float y)
    {
        _pointerX = Math.Clamp(x, -1f, 1f);
        _pointerY = Math.Clamp(y, -1f, 1f);
    }

    public void Update(double seconds)
    {
        var orbit = OrbitAmplitudeRadians * MathF.Sin((float)(seconds / OrbitPeriodSeconds) * MathF.PI * 2f);
        var yaw = orbit + _pointerX * ParallaxAmplitudeRadians;
        var pitch = _pointerY * 0.035f;

        var x = MathF.Sin(yaw) * Distance;
        var z = MathF.Cos(yaw) * Distance;
        Camera.Position = new Vec3(x, EyeHeight - pitch, z);
        Camera.Target = new Vec3(0f, 1.15f, -0.55f);
    }
}
