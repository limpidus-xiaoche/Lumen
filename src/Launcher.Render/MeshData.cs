namespace Launcher.Render;

/// <summary>顶点：位置 3 + 颜色 3 + UV 2，stride = 32 字节。</summary>
public sealed class MeshData
{
    public const int FloatsPerVertex = 8;
    public const int StrideBytes = FloatsPerVertex * sizeof(float);
    public const uint Triangles = 0x0004;

    private readonly List<float> _values = new();

    public int VertexCount => _values.Count / FloatsPerVertex;

    public float[] ToArray() => _values.ToArray();

    /// <summary>合并另一个网格的顶点（要求顶点布局相同）。</summary>
    public void Append(MeshData other) => _values.AddRange(other._values);

    public void AddVertex(Vec3 p, Vec3 c, float u = 0f, float v = 0f)
    {
        _values.Add(p.X);
        _values.Add(p.Y);
        _values.Add(p.Z);
        _values.Add(c.X);
        _values.Add(c.Y);
        _values.Add(c.Z);
        _values.Add(u);
        _values.Add(v);
    }

    public void AddQuad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, Vec3 color)
    {
        AddVertex(a, color);
        AddVertex(b, color);
        AddVertex(c, color);
        AddVertex(a, color);
        AddVertex(c, color);
        AddVertex(d, color);
    }

    /// <summary>带 UV 的四边形，UV 顺序对应 (左上, 左下, 右下, 右上)。</summary>
    public void AddTexturedQuad(Vec3 a, Vec3 b, Vec3 c, Vec3 d, Vec3 color, float u0, float v0, float u1, float v1)
    {
        AddVertex(a, color, u0, v1);
        AddVertex(b, color, u0, v0);
        AddVertex(c, color, u1, v0);
        AddVertex(a, color, u0, v1);
        AddVertex(c, color, u1, v0);
        AddVertex(d, color, u1, v1);
    }

    /// <summary>轴对齐立方体，按面做亮度差，避免纯色平板。</summary>
    public void AddBox(Vec3 center, Vec3 half, Vec3 color)
    {
        var min = center - half;
        var max = center + half;

        var top = color * 1.18f;
        var side = color * 0.86f;
        var bottom = color * 0.55f;

        AddQuad(
            new Vec3(min.X, max.Y, min.Z), new Vec3(min.X, max.Y, max.Z),
            new Vec3(max.X, max.Y, max.Z), new Vec3(max.X, max.Y, min.Z), top);
        AddQuad(
            new Vec3(min.X, min.Y, min.Z), new Vec3(max.X, min.Y, min.Z),
            new Vec3(max.X, min.Y, max.Z), new Vec3(min.X, min.Y, max.Z), bottom);
        AddQuad(
            new Vec3(min.X, min.Y, min.Z), new Vec3(min.X, max.Y, min.Z),
            new Vec3(max.X, max.Y, min.Z), new Vec3(max.X, min.Y, min.Z), side);
        AddQuad(
            new Vec3(max.X, min.Y, max.Z), new Vec3(max.X, max.Y, max.Z),
            new Vec3(min.X, max.Y, max.Z), new Vec3(min.X, min.Y, max.Z), side);
        AddQuad(
            new Vec3(min.X, min.Y, max.Z), new Vec3(min.X, max.Y, max.Z),
            new Vec3(min.X, max.Y, min.Z), new Vec3(min.X, min.Y, min.Z), side);
        AddQuad(
            new Vec3(max.X, min.Y, min.Z), new Vec3(max.X, max.Y, min.Z),
            new Vec3(max.X, max.Y, max.Z), new Vec3(max.X, min.Y, max.Z), side);
    }
}
