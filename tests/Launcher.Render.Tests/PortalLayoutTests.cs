using Launcher.Render;
using Xunit;

namespace Launcher.Render.Tests;

public sealed class PortalLayoutTests
{
    private const float Tolerance = 1e-5f;

    [Fact]
    public void Compute_ZeroInstances_ReturnsEmpty()
    {
        Assert.Empty(PortalLayout.Compute(0));
    }

    [Fact]
    public void Compute_NegativeInstances_ReturnsEmpty()
    {
        Assert.Empty(PortalLayout.Compute(-3));
    }

    [Fact]
    public void Compute_OneInstance_IsCenteredPrimaryDoor()
    {
        var slots = PortalLayout.Compute(1);

        var slot = Assert.Single(slots);
        AssertNear(0f, slot.Position.X);
        AssertNear(0f, slot.Position.Y);
        AssertNear(0f, slot.Position.Z);
        AssertNear(0f, slot.YawRadians);
        AssertNear(1f, slot.Scale);
        AssertNear(PortalLayout.MaxOpacity, slot.Opacity);
        Assert.False(slot.IsMore);
    }

    [Fact]
    public void Compute_ThreeInstances_ReturnsThreeAndAlternatesSides()
    {
        var slots = PortalLayout.Compute(3);

        Assert.Equal(3, slots.Count);
        AssertNear(0f, slots[0].Position.X);
        Assert.True(slots[1].Position.X > 0f, "第 2 扇门应在主门右侧");
        Assert.True(slots[2].Position.X < 0f, "第 3 扇门应在主门左侧");
        Assert.All(slots, slot => Assert.False(slot.IsMore));
    }

    [Fact]
    public void Compute_SevenInstances_ReturnsSevenWithoutMoreSlot()
    {
        var slots = PortalLayout.Compute(7);

        Assert.Equal(PortalLayout.MaxVisible, slots.Count);
        Assert.Equal(7, slots.Count);
        Assert.All(slots, slot => Assert.False(slot.IsMore));
    }

    [Fact]
    public void Compute_EightInstances_FoldsLastSlotIntoMore()
    {
        var slots = PortalLayout.Compute(8);

        Assert.Equal(PortalLayout.MaxVisible, slots.Count);
        for (var i = 0; i < slots.Count - 1; i++)
        {
            Assert.False(slots[i].IsMore, $"第 {i} 位不应折叠");
        }

        Assert.True(slots[^1].IsMore, "超过 7 扇时最后一位应为「更多」");
    }

    [Fact]
    public void Compute_FullLayout_AlternatesSidesAndRecedes()
    {
        var slots = PortalLayout.Compute(PortalLayout.MaxVisible);

        AssertNear(0f, slots[0].Position.X);
        for (var i = 1; i < slots.Count; i++)
        {
            if (i % 2 == 1)
            {
                Assert.True(slots[i].Position.X > 0f, $"第 {i} 位应在右侧");
            }
            else
            {
                Assert.True(slots[i].Position.X < 0f, $"第 {i} 位应在左侧");
            }

            Assert.True(slots[i].Position.Z < slots[i - 1].Position.Z,
                $"第 {i} 位应比前一位更远");
            Assert.True(slots[i].Scale < slots[i - 1].Scale,
                $"第 {i} 位应比前一位更小");
            Assert.True(slots[i].YawRadians * slots[i].Position.X < 0f,
                "门朝向应与弧形展开方向相反");
        }
    }

    [Fact]
    public void Compute_Opacity_StaysWithinDocumentedClamp()
    {
        Assert.Equal(0.35f, PortalLayout.MinOpacity);
        Assert.Equal(0.9f, PortalLayout.MaxOpacity);

        var slots = PortalLayout.Compute(PortalLayout.MaxVisible);
        AssertNear(PortalLayout.MaxOpacity, slots[0].Opacity);

        var previous = slots[0].Opacity;
        foreach (var slot in slots)
        {
            Assert.InRange(slot.Opacity, PortalLayout.MinOpacity, PortalLayout.MaxOpacity);
            Assert.True(slot.Opacity <= previous + Tolerance, "越靠外不透明度不应升高");
            previous = slot.Opacity;
        }
    }

    [Fact]
    public void Compute_ScaleFalloff_UsesHalfStepExponent()
    {
        var slots = PortalLayout.Compute(PortalLayout.MaxVisible);

        AssertNear(1f, slots[0].Scale);
        AssertNear(MathF.Pow(0.80f, 0.5f), slots[1].Scale);
        AssertNear(MathF.Pow(0.80f, 0.75f), slots[2].Scale);
        AssertNear(MathF.Pow(0.80f, 1.75f), slots[^1].Scale);
    }

    private static void AssertNear(float expected, float actual)
    {
        Assert.True(MathF.Abs(expected - actual) <= Tolerance,
            $"期望 {expected}，实际 {actual}");
    }
}
