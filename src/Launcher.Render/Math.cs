using System.Runtime.CompilerServices;

namespace Launcher.Render;

public readonly struct Vec3
{
    public readonly float X, Y, Z;

    public Vec3(float x, float y, float z)
    {
        X = x;
        Y = y;
        Z = z;
    }

    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 UnitY = new(0, 1, 0);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator *(Vec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    public float Length => MathF.Sqrt(X * X + Y * Y + Z * Z);

    public Vec3 Normalized()
    {
        var len = Length;
        return len <= 1e-6f ? Zero : new Vec3(X / len, Y / len, Z / len);
    }

    public static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;
}

/// <summary>列主序 4x4 矩阵，与 OpenGL 一致。</summary>
public struct Mat4
{
    public float M11, M12, M13, M14;
    public float M21, M22, M23, M24;
    public float M31, M32, M33, M34;
    public float M41, M42, M43, M44;

    public static Mat4 Identity => new()
    {
        M11 = 1, M22 = 1, M33 = 1, M44 = 1,
    };

    public static Mat4 Perspective(float fovYDegrees, float aspect, float near, float far)
    {
        var f = 1f / MathF.Tan(fovYDegrees * MathF.PI / 360f);
        var m = Identity;
        m.M11 = f / aspect;
        m.M22 = f;
        m.M33 = (far + near) / (near - far);
        m.M34 = -1f;
        m.M43 = 2f * far * near / (near - far);
        m.M44 = 0f;
        return m;
    }

    public static Mat4 LookAt(Vec3 eye, Vec3 target, Vec3 up)
    {
        var zAxis = (eye - target).Normalized();
        var xAxis = Vec3.Cross(up, zAxis).Normalized();
        var yAxis = Vec3.Cross(zAxis, xAxis);

        var m = Identity;
        m.M11 = xAxis.X; m.M12 = yAxis.X; m.M13 = zAxis.X; m.M14 = 0;
        m.M21 = xAxis.Y; m.M22 = yAxis.Y; m.M23 = zAxis.Y; m.M24 = 0;
        m.M31 = xAxis.Z; m.M32 = yAxis.Z; m.M33 = zAxis.Z; m.M34 = 0;
        m.M41 = -Vec3.Dot(xAxis, eye);
        m.M42 = -Vec3.Dot(yAxis, eye);
        m.M43 = -Vec3.Dot(zAxis, eye);
        m.M44 = 1;
        return m;
    }

    public static Mat4 Translation(Vec3 t)
    {
        var m = Identity;
        m.M41 = t.X; m.M42 = t.Y; m.M43 = t.Z;
        return m;
    }

    public static Mat4 RotationY(float radians)
    {
        var c = MathF.Cos(radians);
        var s = MathF.Sin(radians);
        var m = Identity;
        m.M11 = c; m.M13 = -s;
        m.M31 = s; m.M33 = c;
        return m;
    }

    public static Mat4 Scale(float s)
    {
        var m = Identity;
        m.M11 = s; m.M22 = s; m.M33 = s;
        return m;
    }

    /// <summary>列主序写入，供 glUniformMatrix4fv 直接使用。</summary>
    public void CopyTo(Span<float> dst)
    {
        dst[0] = M11; dst[1] = M12; dst[2] = M13; dst[3] = M14;
        dst[4] = M21; dst[5] = M22; dst[6] = M23; dst[7] = M24;
        dst[8] = M31; dst[9] = M32; dst[10] = M33; dst[11] = M34;
        dst[12] = M41; dst[13] = M42; dst[14] = M43; dst[15] = M44;
    }

    /// <summary>a * b（先应用 b 再应用 a）。</summary>
    public static Mat4 operator *(Mat4 a, Mat4 b)
    {
        var r = default(Mat4);
        r.M11 = RowDot(a, 0, b, 0); r.M12 = RowDot(a, 0, b, 1); r.M13 = RowDot(a, 0, b, 2); r.M14 = RowDot(a, 0, b, 3);
        r.M21 = RowDot(a, 1, b, 0); r.M22 = RowDot(a, 1, b, 1); r.M23 = RowDot(a, 1, b, 2); r.M24 = RowDot(a, 1, b, 3);
        r.M31 = RowDot(a, 2, b, 0); r.M32 = RowDot(a, 2, b, 1); r.M33 = RowDot(a, 2, b, 2); r.M34 = RowDot(a, 2, b, 3);
        r.M41 = RowDot(a, 3, b, 0); r.M42 = RowDot(a, 3, b, 1); r.M43 = RowDot(a, 3, b, 2); r.M44 = RowDot(a, 3, b, 3);
        return r;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float RowDot(Mat4 a, int row, Mat4 b, int col)
    {
        ref var p = ref Unsafe.As<Mat4, float>(ref Unsafe.AsRef(in a));
        ref var q = ref Unsafe.As<Mat4, float>(ref Unsafe.AsRef(in b));
        return Unsafe.Add(ref p, row) * Unsafe.Add(ref q, col * 4)
             + Unsafe.Add(ref p, row + 4) * Unsafe.Add(ref q, col * 4 + 1)
             + Unsafe.Add(ref p, row + 8) * Unsafe.Add(ref q, col * 4 + 2)
             + Unsafe.Add(ref p, row + 12) * Unsafe.Add(ref q, col * 4 + 3);
    }
}
