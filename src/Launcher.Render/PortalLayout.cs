namespace Launcher.Render;

/// <summary>一扇门在世界中的位姿。</summary>
public readonly record struct PortalSlot(
    Vec3 Position,
    float YawRadians,
    float Scale,
    float Opacity,
    bool IsMore);

/// <summary>门的弧形排布，纯函数，便于单测。</summary>
public static class PortalLayout
{
    public const int MaxVisible = 7;
    public const float ArcRadius = 3.2f;
    public const float ArcStepRadians = 30f * MathF.PI / 180f;
    public const float DepthStep = 0.55f;
    public const float MinOpacity = 0.35f;
    public const float MaxOpacity = 0.9f;

    public static IReadOnlyList<PortalSlot> Compute(int instanceCount)
    {
        if (instanceCount <= 0)
        {
            return [];
        }

        var visible = Math.Min(instanceCount, MaxVisible);
        var slots = new PortalSlot[visible];
        var hasMore = instanceCount > MaxVisible;

        for (var i = 0; i < visible; i++)
        {
            // 以主门为中心左右交替展开：1→右, 2→左, 3→右二, 4→左二……
            var k = i == 0 ? 0d : (i + 1) / 2d * (i % 2 == 1 ? 1 : -1);
            var isMore = hasMore && i == MaxVisible - 1;
            var angle = (float)k * ArcStepRadians;
            var distanceFactor = Math.Abs(k);

            var x = MathF.Sin(angle) * ArcRadius;
            var z = -(1f - MathF.Cos(angle)) * ArcRadius - (float)distanceFactor * DepthStep;
            var y = -0.04f * (float)distanceFactor;

            slots[i] = new PortalSlot(
                new Vec3(x, y, z),
                -angle,
                MathF.Pow(0.80f, (float)distanceFactor / 2f),
                Math.Clamp(MaxOpacity - 0.10f * (float)distanceFactor, MinOpacity, MaxOpacity),
                isMore);
        }

        return slots;
    }
}
