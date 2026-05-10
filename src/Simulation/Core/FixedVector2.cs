using System;
using System.Runtime.CompilerServices;

namespace WarOfKings.Simulation.Core;

/// <summary>
/// 2D vector using Fixed64 components. The standard position and velocity type for the simulation.
/// </summary>
public readonly struct FixedVector2 : IEquatable<FixedVector2>
{
    public readonly Fixed64 X;
    public readonly Fixed64 Y;

    public FixedVector2(Fixed64 x, Fixed64 y) { X = x; Y = y; }

    public static readonly FixedVector2 Zero = new(Fixed64.Zero, Fixed64.Zero);
    public static readonly FixedVector2 UnitX = new(Fixed64.OneValue, Fixed64.Zero);
    public static readonly FixedVector2 UnitY = new(Fixed64.Zero, Fixed64.OneValue);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedVector2 FromInts(int x, int y) => new(Fixed64.FromInt(x), Fixed64.FromInt(y));

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedVector2 operator +(FixedVector2 a, FixedVector2 b) => new(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedVector2 operator -(FixedVector2 a, FixedVector2 b) => new(a.X - b.X, a.Y - b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedVector2 operator *(FixedVector2 v, Fixed64 s) => new(v.X * s, v.Y * s);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static FixedVector2 operator /(FixedVector2 v, Fixed64 s) => new(v.X / s, v.Y / s);

    public Fixed64 LengthSquared() => X * X + Y * Y;

    /// <summary>
    /// True 2D distance. Uses FixedMath.Sqrt; if you only need to compare distances,
    /// use LengthSquared() and compare against the square instead. Faster and exact.
    /// </summary>
    public Fixed64 Length() => FixedMath.Sqrt(LengthSquared());

    public FixedVector2 Normalized()
    {
        var len = Length();
        if (len == Fixed64.Zero) return Zero;
        return new(X / len, Y / len);
    }

    public static Fixed64 Distance(FixedVector2 a, FixedVector2 b) => (a - b).Length();
    public static Fixed64 DistanceSquared(FixedVector2 a, FixedVector2 b) => (a - b).LengthSquared();

    public bool Equals(FixedVector2 other) => X == other.X && Y == other.Y;
    public override bool Equals(object? obj) => obj is FixedVector2 v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(X.Raw, Y.Raw);
    public override string ToString() => $"({X}, {Y})";

    public static bool operator ==(FixedVector2 a, FixedVector2 b) => a.Equals(b);
    public static bool operator !=(FixedVector2 a, FixedVector2 b) => !a.Equals(b);
}
