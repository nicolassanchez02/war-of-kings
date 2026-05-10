using WarOfKings.Simulation.Core;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class FixedMathTests
{
    // ----- Sqrt -----

    [Fact]
    public void Sqrt_Zero_IsZero()
    {
        Assert.Equal(Fixed64.Zero, FixedMath.Sqrt(Fixed64.Zero));
    }

    [Fact]
    public void Sqrt_One_IsOne()
    {
        Assert.Equal(Fixed64.OneValue, FixedMath.Sqrt(Fixed64.OneValue));
    }

    [Fact]
    public void Sqrt_Four_IsTwo()
    {
        Assert.Equal(Fixed64.FromInt(2), FixedMath.Sqrt(Fixed64.FromInt(4)));
    }

    [Fact]
    public void Sqrt_Nine_IsThree()
    {
        Assert.Equal(Fixed64.FromInt(3), FixedMath.Sqrt(Fixed64.FromInt(9)));
    }

    [Fact]
    public void Sqrt_OneMillion_IsThousand()
    {
        Assert.Equal(Fixed64.FromInt(1000), FixedMath.Sqrt(Fixed64.FromInt(1_000_000)));
    }

    // Canonical pinned values. Output is floor(sqrt(raw * 2^16)), exact for these inputs.
    // Changing these breaks every replay; treat as a deliberate, versioned event.

    [Fact]
    public void Sqrt_Two_HasPinnedRaw()
    {
        // sqrt(2) ~ 1.41421356, * 65536 = 92681.9; floor = 92681.
        Assert.Equal(92681L, FixedMath.Sqrt(Fixed64.FromInt(2)).Raw);
    }

    [Fact]
    public void Sqrt_Half_HasPinnedRaw()
    {
        // sqrt(0.5) ~ 0.7071068, * 65536 = 46340.95; floor = 46340.
        Assert.Equal(46340L, FixedMath.Sqrt(Fixed64.FromFraction(1, 2)).Raw);
    }

    [Fact]
    public void Sqrt_Three_HasPinnedRaw()
    {
        // sqrt(3) ~ 1.7320508, * 65536 = 113511.69; floor = 113511.
        Assert.Equal(113511L, FixedMath.Sqrt(Fixed64.FromInt(3)).Raw);
    }

    [Fact]
    public void Sqrt_Large_StaysInRange()
    {
        // sqrt(10^9) ~ 31622.776, * 65536 = ...
        // Just check it's in the expected ballpark (Raw close to sqrt(10^9) * 65536).
        var s = FixedMath.Sqrt(Fixed64.FromInt(1_000_000_000));
        // Expected ToInt ~ 31622
        Assert.InRange(s.ToInt(), 31622, 31623);
    }

    [Fact]
    public void Sqrt_Negative_Throws()
    {
        Assert.Throws<System.ArgumentException>(() => FixedMath.Sqrt(Fixed64.FromInt(-1)));
    }

    // ----- Sin and Cos -----

    private static readonly Fixed64 SinTolerance = Fixed64.FromRaw(50); // ~7.6e-4

    [Fact]
    public void Sin_Zero_IsZero()
    {
        Assert.Equal(Fixed64.Zero, FixedMath.Sin(Fixed64.Zero));
    }

    [Fact]
    public void Sin_HalfPi_IsApproxOne()
    {
        var d = (FixedMath.Sin(FixedMath.HalfPi) - Fixed64.OneValue).Abs();
        Assert.True(d <= SinTolerance, $"|sin(pi/2) - 1| = {d.Raw} raw units");
    }

    [Fact]
    public void Sin_Pi_IsApproxZero()
    {
        var d = FixedMath.Sin(FixedMath.Pi).Abs();
        Assert.True(d <= SinTolerance, $"|sin(pi)| = {d.Raw} raw units");
    }

    [Fact]
    public void Sin_NegHalfPi_IsApproxMinusOne()
    {
        var d = (FixedMath.Sin(-FixedMath.HalfPi) - Fixed64.MinusOne).Abs();
        Assert.True(d <= SinTolerance, $"|sin(-pi/2) + 1| = {d.Raw} raw units");
    }

    [Fact]
    public void Sin_TwoPi_IsApproxZero()
    {
        var d = FixedMath.Sin(FixedMath.TwoPi).Abs();
        Assert.True(d <= SinTolerance, $"|sin(2pi)| = {d.Raw} raw units");
    }

    [Fact]
    public void Cos_Zero_IsOne()
    {
        var d = (FixedMath.Cos(Fixed64.Zero) - Fixed64.OneValue).Abs();
        Assert.True(d <= SinTolerance, $"|cos(0) - 1| = {d.Raw} raw units");
    }

    [Fact]
    public void Cos_HalfPi_IsApproxZero()
    {
        var d = FixedMath.Cos(FixedMath.HalfPi).Abs();
        Assert.True(d <= SinTolerance, $"|cos(pi/2)| = {d.Raw} raw units");
    }

    [Fact]
    public void Cos_Pi_IsApproxMinusOne()
    {
        var d = (FixedMath.Cos(FixedMath.Pi) - Fixed64.MinusOne).Abs();
        Assert.True(d <= SinTolerance, $"|cos(pi) + 1| = {d.Raw} raw units");
    }

    [Fact]
    public void SinSquaredPlusCosSquared_IsApproxOne_AcrossDomain()
    {
        // sin^2 + cos^2 = 1 for any x. Check at 16 sample angles.
        for (int i = 0; i < 16; i++)
        {
            var x = FixedMath.TwoPi * Fixed64.FromFraction(i, 16);
            var s = FixedMath.Sin(x);
            var c = FixedMath.Cos(x);
            var d = (s * s + c * c - Fixed64.OneValue).Abs();
            Assert.True(d <= Fixed64.FromRaw(200),
                $"|sin^2 + cos^2 - 1| at x.Raw={x.Raw}: {d.Raw} raw units");
        }
    }

    // ----- Atan2 -----

    private static readonly Fixed64 AtanTolerance = Fixed64.FromRaw(50); // ~7.6e-4 radians

    [Fact]
    public void Atan2_PositiveX_IsZero()
    {
        Assert.Equal(Fixed64.Zero, FixedMath.Atan2(Fixed64.Zero, Fixed64.OneValue));
    }

    [Fact]
    public void Atan2_PositiveY_IsHalfPi()
    {
        Assert.Equal(FixedMath.HalfPi, FixedMath.Atan2(Fixed64.OneValue, Fixed64.Zero));
    }

    [Fact]
    public void Atan2_NegativeX_IsPi()
    {
        Assert.Equal(FixedMath.Pi, FixedMath.Atan2(Fixed64.Zero, Fixed64.MinusOne));
    }

    [Fact]
    public void Atan2_NegativeY_IsMinusHalfPi()
    {
        Assert.Equal(-FixedMath.HalfPi, FixedMath.Atan2(Fixed64.MinusOne, Fixed64.Zero));
    }

    [Fact]
    public void Atan2_OneOne_IsQuarterPi()
    {
        var a = FixedMath.Atan2(Fixed64.OneValue, Fixed64.OneValue);
        var d = (a - FixedMath.QuarterPi).Abs();
        Assert.True(d <= AtanTolerance, $"|atan2(1,1) - pi/4| = {d.Raw} raw units");
    }

    [Fact]
    public void Atan2_MinusOneMinusOne_IsMinusThreeQuarterPi()
    {
        var a = FixedMath.Atan2(Fixed64.MinusOne, Fixed64.MinusOne);
        var expected = -(FixedMath.Pi - FixedMath.QuarterPi); // -3pi/4
        var d = (a - expected).Abs();
        Assert.True(d <= AtanTolerance, $"|atan2(-1,-1) - (-3pi/4)| = {d.Raw} raw units");
    }

    [Fact]
    public void Atan2_RoundTrip_With_SinCos()
    {
        // For 8 directions, atan2(sin a, cos a) ~ a.
        for (int i = 0; i < 8; i++)
        {
            var a = FixedMath.TwoPi * Fixed64.FromFraction(i, 8) - FixedMath.Pi;
            // Skip exactly -Pi to avoid the branch-cut boundary (atan2 returns +Pi there).
            if (a.Raw == -FixedMath.Pi.Raw) continue;
            var s = FixedMath.Sin(a);
            var c = FixedMath.Cos(a);
            var recovered = FixedMath.Atan2(s, c);
            var d = (recovered - a).Abs();
            Assert.True(d <= Fixed64.FromRaw(200),
                $"atan2(sin a, cos a) - a at a.Raw={a.Raw}: {d.Raw} raw units");
        }
    }
}
