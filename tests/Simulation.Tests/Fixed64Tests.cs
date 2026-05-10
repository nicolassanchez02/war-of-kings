using WarOfKings.Simulation.Core;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class Fixed64Tests
{
    [Fact]
    public void Zero_IsZero()
    {
        Assert.Equal(0L, Fixed64.Zero.Raw);
        Assert.Equal(0, Fixed64.Zero.ToInt());
    }

    [Fact]
    public void FromInt_RoundTrips()
    {
        Assert.Equal(42, Fixed64.FromInt(42).ToInt());
        Assert.Equal(-17, Fixed64.FromInt(-17).ToInt());
        Assert.Equal(0, Fixed64.FromInt(0).ToInt());
    }

    [Fact]
    public void Addition_Works()
    {
        var a = Fixed64.FromInt(5);
        var b = Fixed64.FromInt(3);
        Assert.Equal(8, (a + b).ToInt());
    }

    [Fact]
    public void Multiplication_Works()
    {
        var a = Fixed64.FromInt(5);
        var b = Fixed64.FromInt(3);
        Assert.Equal(15, (a * b).ToInt());
    }

    [Fact]
    public void Multiplication_WithFraction_Works()
    {
        var half = Fixed64.FromFraction(1, 2);
        var ten = Fixed64.FromInt(10);
        Assert.Equal(5, (half * ten).ToInt());
    }

    [Fact]
    public void Division_Works()
    {
        var ten = Fixed64.FromInt(10);
        var two = Fixed64.FromInt(2);
        Assert.Equal(5, (ten / two).ToInt());
    }

    [Fact]
    public void Comparison_Works()
    {
        Assert.True(Fixed64.FromInt(5) > Fixed64.FromInt(3));
        Assert.True(Fixed64.FromInt(3) < Fixed64.FromInt(5));
        Assert.True(Fixed64.FromInt(5) == Fixed64.FromInt(5));
        Assert.True(Fixed64.FromInt(5) != Fixed64.FromInt(6));
    }

    [Fact]
    public void Negation_Works()
    {
        var x = Fixed64.FromInt(7);
        Assert.Equal(-7, (-x).ToInt());
    }

    [Fact]
    public void Abs_Works()
    {
        Assert.Equal(5, Fixed64.FromInt(-5).Abs().ToInt());
        Assert.Equal(5, Fixed64.FromInt(5).Abs().ToInt());
    }

    // TODO(Claude Code, M0): add a test that asserts identical Raw values across .NET 8
    // on Windows, Linux, macOS for a fixed sequence of operations. CI matrix should run this.
}
