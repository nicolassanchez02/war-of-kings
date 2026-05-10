using System;

namespace WarOfKings.Simulation.Core;

/// <summary>
/// Deterministic math functions returning Fixed64.
///
/// IMPORTANT: Do not call System.Math.Sin/Cos/Sqrt/etc in simulation code.
/// They produce platform-dependent results and will desync multiplayer or break replays.
///
/// Precision target: absolute error within a few raw units (1 raw = 1/65536 ~ 1.5e-5).
/// All functions are pure, allocation-free, and platform-independent.
/// </summary>
public static class FixedMath
{
    // Pi ~ 3.14159265, in raw: 3.14159265 * 65536 = 205887.4..., we round down to 205887.
    public static readonly Fixed64 Pi = Fixed64.FromRaw(205887);
    public static readonly Fixed64 TwoPi = Fixed64.FromRaw(411774);   // 2 * Pi.Raw, consistent with Pi
    public static readonly Fixed64 HalfPi = Fixed64.FromRaw(102944);  // round-half-up of Pi.Raw / 2
    public static readonly Fixed64 QuarterPi = Fixed64.FromRaw(51472);

    /// <summary>
    /// Square root via shift-and-subtract integer sqrt of (raw &lt;&lt; FractionalBits).
    /// Returns floor of the true square root in Q.16 representation.
    /// Deterministic across platforms: uses only integer ops on Int128.
    /// Throws for negative inputs.
    /// </summary>
    public static Fixed64 Sqrt(Fixed64 value)
    {
        if (value.Raw < 0)
            throw new ArgumentException("Sqrt of negative", nameof(value));
        if (value.Raw == 0)
            return Fixed64.Zero;

        // For Fixed64 v = R / 2^16, sqrt(v) = sqrt(R) / 2^8.
        // In Q.16 raw: output = sqrt(R) * 2^8 = sqrt(R * 2^16).
        // R is non-negative long (up to 2^63 - 1), so R << 16 needs up to 79 bits.
        UInt128 n = ((UInt128)(ulong)value.Raw) << Fixed64.FractionalBits;

        // Shift-and-subtract integer sqrt. Bit must start at the highest power of 4 <= n.
        // n <= (2^63 - 1) << 16 < 2^79, so bit = 1 << 78 covers it.
        UInt128 res = UInt128.Zero;
        UInt128 bit = ((UInt128)1) << 78;
        while (bit > n) bit >>= 2;

        while (bit != UInt128.Zero)
        {
            var trial = res + bit;
            if (n >= trial)
            {
                n -= trial;
                res = (res >> 1) + bit;
            }
            else
            {
                res >>= 1;
            }
            bit >>= 2;
        }

        return Fixed64.FromRaw((long)res);
    }

    /// <summary>
    /// Sine of an angle in radians. Argument is reduced to [-pi, pi] via modular arithmetic
    /// on the raw representation, then to [-pi/2, pi/2] using sin(pi - x) = sin(x).
    /// Approximated by a 4-term Taylor polynomial in Horner form.
    /// </summary>
    public static Fixed64 Sin(Fixed64 radians)
    {
        // Reduce to (-TwoPi, TwoPi) by modulo on raw.
        long rawMod = radians.Raw % TwoPi.Raw;
        // Reduce to (-Pi, Pi].
        if (rawMod > Pi.Raw) rawMod -= TwoPi.Raw;
        else if (rawMod < -Pi.Raw) rawMod += TwoPi.Raw;

        // Reduce to [-HalfPi, HalfPi] using sin(pi - x) = sin(x) and sin(-pi - x) = sin(x).
        if (rawMod > HalfPi.Raw) rawMod = Pi.Raw - rawMod;
        else if (rawMod < -HalfPi.Raw) rawMod = -Pi.Raw - rawMod;

        var x = Fixed64.FromRaw(rawMod);
        var x2 = x * x;

        // Horner: sin(x) ~ x * (1 - x^2*(1/6 - x^2*(1/120 - x^2/5040)))
        // Constants in Q.16: 1/6 = 10923, 1/120 = 546, 1/5040 = 13. Next term 1/362880 ~ 0 in Q.16.
        var c5040 = Fixed64.FromRaw(13);
        var c120 = Fixed64.FromRaw(546);
        var c6 = Fixed64.FromRaw(10923);

        var term = c120 - x2 * c5040;
        term = c6 - x2 * term;
        term = Fixed64.OneValue - x2 * term;
        return x * term;
    }

    /// <summary>Cosine of an angle in radians. Implemented as Sin(x + pi/2).</summary>
    public static Fixed64 Cos(Fixed64 radians) => Sin(radians + HalfPi);

    /// <summary>
    /// Two-argument arctangent in [-pi, pi]. Handles all four quadrants.
    /// Reduces |y/x| to [0, 1] via swap, then to [0, tan(pi/8)] via the identity
    /// atan(r) = pi/4 + atan((r-1)/(r+1)). Final stretch uses a 5-term Taylor series.
    /// </summary>
    public static Fixed64 Atan2(Fixed64 y, Fixed64 x)
    {
        if (x.Raw == 0)
        {
            if (y.Raw > 0) return HalfPi;
            if (y.Raw < 0) return -HalfPi;
            return Fixed64.Zero;
        }

        bool yNeg = y.Raw < 0;
        bool xNeg = x.Raw < 0;
        var absY = y.Abs();
        var absX = x.Abs();

        bool swap = absY.Raw > absX.Raw;
        var ratio = swap ? (absX / absY) : (absY / absX);

        var angle = AtanReduced(ratio);     // result in [0, pi/4]
        if (swap) angle = HalfPi - angle;   // map to [pi/4, pi/2]
        if (xNeg) angle = Pi - angle;       // reflect into left half-plane
        if (yNeg) angle = -angle;           // mirror across x axis

        return angle;
    }

    // atan(r) for r in [0, 1]. Reduces to [0, tan(pi/8)] before a 5-term Taylor.
    // tan(pi/8) = sqrt(2) - 1 ~ 0.4142135. In Q.16 raw: 27146.
    private static Fixed64 AtanReduced(Fixed64 r)
    {
        var tanPi8 = Fixed64.FromRaw(27146);
        bool reduced = false;
        if (r.Raw > tanPi8.Raw)
        {
            r = (r - Fixed64.OneValue) / (r + Fixed64.OneValue);
            reduced = true;
        }

        var r2 = r * r;
        // Horner: atan(r) ~ r * (1 - r^2*(1/3 - r^2*(1/5 - r^2*(1/7 - r^2/9))))
        var c9 = Fixed64.FromFraction(1, 9);
        var c7 = Fixed64.FromFraction(1, 7);
        var c5 = Fixed64.FromFraction(1, 5);
        var c3 = Fixed64.FromFraction(1, 3);

        var term = c7 - r2 * c9;
        term = c5 - r2 * term;
        term = c3 - r2 * term;
        term = Fixed64.OneValue - r2 * term;
        var result = r * term;

        if (reduced) result = QuarterPi + result; // atan(r_new) is negative when r_orig < 1
        return result;
    }
}
