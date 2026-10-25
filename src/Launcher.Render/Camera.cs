namespace Launcher.Render;

/// <summary>世界坐标单位为米；投影与布局参数全部按米定义。</summary>
public sealed class Camera
{
    public Vec3 Position { get; set; } = new(0f, 2.2f, 8.5f);
    public Vec3 Target { get; set; } = new(0f, 0.8f, 0f);
    public float FovYDegrees { get; set; } = 45f;
    public float Near { get; set; } = 0.1f;
    public float Far { get; set; } = 100f;

    public Mat4 ViewMatrix() => Mat4.LookAt(Position, Target, Vec3.UnitY);

    public Mat4 ProjectionMatrix(float aspect) => Mat4.Perspective(FovYDegrees, aspect, Near, Far);

    public Mat4 ViewProjection(float aspect) => ProjectionMatrix(aspect) * ViewMatrix();
}
