using Launcher.Render;
using Xunit;

namespace Launcher.Render.Tests;

public sealed class CameraTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Defaults_MatchWorldLayerFraming()
    {
        var camera = new Camera();

        AssertNear(0f, camera.Position.X);
        AssertNear(2.2f, camera.Position.Y);
        AssertNear(8.5f, camera.Position.Z);

        AssertNear(0f, camera.Target.X);
        AssertNear(0.8f, camera.Target.Y);
        AssertNear(0f, camera.Target.Z);

        AssertNear(45f, camera.FovYDegrees);
        AssertNear(0.1f, camera.Near);
        AssertNear(100f, camera.Far);
    }

    [Fact]
    public void ViewMatrix_IsFiniteAndKeepsRowMajorHomogeneousRow()
    {
        var view = new Camera().ViewMatrix();

        AssertFinite(view);
        AssertNear(1f, view.M44);
        AssertNear(0f, view.M14);
        AssertNear(0f, view.M24);
        AssertNear(0f, view.M34);
    }

    [Fact]
    public void ProjectionMatrix_ForWideAspect_MatchesFovAndDepthRange()
    {
        var camera = new Camera();
        var aspect = 16f / 9f;
        var projection = camera.ProjectionMatrix(aspect);

        AssertFinite(projection);

        // f = 1 / tan(fovY / 2)，fovY = 45°
        var f = 1f / MathF.Tan(45f * MathF.PI / 360f);
        AssertNear(f / aspect, projection.M11);
        AssertNear(f, projection.M22);
        AssertNear((camera.Far + camera.Near) / (camera.Near - camera.Far), projection.M33);
        AssertNear(-1f, projection.M34);
        AssertNear(2f * camera.Far * camera.Near / (camera.Near - camera.Far), projection.M43);
        AssertNear(0f, projection.M44);

        Assert.True(projection.M11 > 0f, "M11 必须非零");
        Assert.True(projection.M22 > 0f, "M22 必须非零");
        Assert.True(projection.M11 < projection.M22, "宽屏下横向缩放应小于纵向");
    }

    [Theory]
    [InlineData(16f / 9f)]
    [InlineData(4f / 3f)]
    [InlineData(1f)]
    [InlineData(21f / 9f)]
    public void ProjectionMatrix_IsFiniteForCommonAspects(float aspect)
    {
        var projection = new Camera().ProjectionMatrix(aspect);

        AssertFinite(projection);
        Assert.NotEqual(0f, projection.M11);
        Assert.NotEqual(0f, projection.M22);
        Assert.NotEqual(0f, projection.M43);
    }

    [Fact]
    public void ViewProjection_IsFiniteAndComposesBothMatrices()
    {
        var camera = new Camera();
        var aspect = 16f / 9f;
        var combined = camera.ViewProjection(aspect);

        AssertFinite(combined);

        var expected = camera.ProjectionMatrix(aspect) * camera.ViewMatrix();
        AssertNear(expected.M11, combined.M11);
        AssertNear(expected.M22, combined.M22);
        AssertNear(expected.M33, combined.M33);
        AssertNear(expected.M43, combined.M43);
    }

    private static void AssertFinite(Mat4 m)
    {
        foreach (var value in ToArray(m))
        {
            Assert.True(float.IsFinite(value), $"矩阵元素不是有限值：{value}");
        }
    }

    private static float[] ToArray(Mat4 m)
    {
        var values = new float[16];
        m.CopyTo(values);
        return values;
    }

    private static void AssertNear(float expected, float actual)
    {
        Assert.True(MathF.Abs(expected - actual) <= Tolerance,
            $"期望 {expected}，实际 {actual}");
    }
}
