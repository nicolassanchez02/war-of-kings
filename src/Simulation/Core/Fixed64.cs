using System;
using System.Runtime.CompilerServices;

namespace WarOfKings.Simulation.Core;

/// <summary>
/// 64-bit signed fixed-point number with 16 fractional bits (Q47.16 effectively, since one bit is sign).
/// Range: approximately +/- 140 trillion. Precision: 1/65536 ~ 0.0000153.
///
/// This type is the foundation of deterministic simulation. Use it instead of float or double
/// anywhere in the simulation layer.
///
/// All operations are deterministic across platforms (no IEEE 754 surprises).
/// </summary>
public readonly struct Fixed64 : IEquatable<Fixed64>, IComparable<Fixed64>
{
    public const int FractionalBits = 16;
    public const long One = 1L << FractionalBits;       // 65536
    public const long Half = One >> 1;                  // 32768
    private const long FractionMask = One - 1;

    public readonly long Raw;

    private Fixed64(long raw) { Raw = raw; }

    public static readonly Fixed64 Zero = new(0);
    public static readonly Fixed64 OneValue = new(One);
    public static readonly Fixed64 MinusOne = new(-One);
    public static readonly Fixed64 MaxValue = new(long.MaxValue);
    public static readonly Fixed64 MinValue = new(long.MinValue);

    // --- Construction ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed64 FromRaw(long raw) => new(raw);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed64 FromInt(int value) => new((long)value << FractionalBits);

    public static Fixed64 FromFraction(int numerator, int denominator)
    {
        if (denominator == 0) throw new DivideByZeroException();
        return new(((long)numerator << FractionalBits) / denominator);
    }

    // --- Conversion ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ToInt() => (int)(Raw >> FractionalBits);

    /// <summary>
    /// Lossy conversion to float, for renderer use only.
    /// Do not use this value in any simulation calculation.
    /// </summary>
    public float ToFloatForRender() => Raw / (float)One;

    public override string ToString() => ToFloatForRender().ToString("0.######");

    // --- Arithmetic ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed64 operator +(Fixed64 a, Fixed64 b) => new(a.Raw + b.Raw);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed64 operator -(Fixed64 a, Fixed64 b) => new(a.Raw - b.Raw);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed64 operator -(Fixed64 a) => new(-a.Raw);

    public static Fixed64 operator *(Fixed64 a, Fixed64 b)
    {
        // Use Int128 (or manual high/low) to avoid overflow during multiplication.
        // .NET 7+ has System.Int128; if targeting older, use manual high/low split.
        var product = (System.Int128)a.Raw * b.Raw;
        return new((long)(product >> FractionalBits));
    }

    public static Fixed64 operator /(Fixed64 a, Fixed64 b)
    {
        if (b.Raw == 0) throw new DivideByZeroException();
        var numerator = (System.Int128)a.Raw << FractionalBits;
        return new((long)(numerator / b.Raw));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed64 operator *(Fixed64 a, int b) => new(a.Raw * b);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Fixed64 operator /(Fixed64 a, int b) => new(a.Raw / b);

    // --- Comparison ---

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(Fixed64 a, Fixed64 b) => a.Raw == b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(Fixed64 a, Fixed64 b) => a.Raw != b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <(Fixed64 a, Fixed64 b) => a.Raw < b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >(Fixed64 a, Fixed64 b) => a.Raw > b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator <=(Fixed64 a, Fixed64 b) => a.Raw <= b.Raw;
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator >=(Fixed64 a, Fixed64 b) => a.Raw >= b.Raw;

    public bool Equals(Fixed64 other) => Raw == other.Raw;
    public override bool Equals(object? obj) => obj is Fixed64 f && Equals(f);
    public override int GetHashCode() => Raw.GetHashCode();
    public int CompareTo(Fixed64 other) => Raw.CompareTo(other.Raw);

    // --- Helpers ---

    public Fixed64 Abs() => Raw < 0 ? new(-Raw) : this;
    public int Sign() => Math.Sign(Raw);

    public static Fixed64 Min(Fixed64 a, Fixed64 b) => a.Raw < b.Raw ? a : b;
    public static Fixed64 Max(Fixed64 a, Fixed64 b) => a.Raw > b.Raw ? a : b;
    public static Fixed64 Clamp(Fixed64 value, Fixed64 min, Fixed64 max)
        => value.Raw < min.Raw ? min : (value.Raw > max.Raw ? max : value);
}
