using System.Runtime.CompilerServices;

namespace WarOfKings.Simulation.Core;

/// <summary>
/// Deterministic seeded RNG. The only RNG allowed in simulation code.
///
/// Implementation: xoshiro256** (well-tested, fast, no platform variance).
/// Behavior is a pure function of (seed, call count).
///
/// Do not share an instance between subsystems if you want their call counts to be independent.
/// Seed each subsystem's RNG from a derived hash of the master seed instead.
/// </summary>
public sealed class DeterministicRng
{
    private ulong _s0, _s1, _s2, _s3;

    public DeterministicRng(ulong seed)
    {
        // SplitMix64 to expand seed into 256 bits of state. Standard practice.
        _s0 = SplitMix64(ref seed);
        _s1 = SplitMix64(ref seed);
        _s2 = SplitMix64(ref seed);
        _s3 = SplitMix64(ref seed);
    }

    private static ulong SplitMix64(ref ulong x)
    {
        x += 0x9E3779B97F4A7C15UL;
        var z = x;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong RotL(ulong x, int k) => (x << k) | (x >> (64 - k));

    public ulong NextULong()
    {
        var result = RotL(_s1 * 5, 7) * 9;
        var t = _s1 << 17;

        _s2 ^= _s0;
        _s3 ^= _s1;
        _s1 ^= _s2;
        _s0 ^= _s3;
        _s2 ^= t;
        _s3 = RotL(_s3, 45);

        return result;
    }

    public uint NextUInt() => (uint)(NextULong() >> 32);

    /// <summary>
    /// Uniform int in [minInclusive, maxExclusive).
    /// </summary>
    public int NextIntRange(int minInclusive, int maxExclusive)
    {
        if (maxExclusive <= minInclusive)
            throw new System.ArgumentException("max must be greater than min");
        var range = (uint)(maxExclusive - minInclusive);
        // Unbiased: rejection sampling for true uniformity.
        var bound = uint.MaxValue - (uint.MaxValue % range);
        uint r;
        do { r = NextUInt(); } while (r >= bound);
        return minInclusive + (int)(r % range);
    }

    /// <summary>
    /// Fixed64 in [0, 1).
    /// </summary>
    public Fixed64 NextFixed01()
    {
        // Use bottom FractionalBits bits of next ULong, shifted to fractional position.
        // This guarantees the result is in [0, 1).
        var bits = NextULong() & (ulong)(Fixed64.One - 1);
        return Fixed64.FromRaw((long)bits);
    }

    /// <summary>
    /// Mix the internal state into a hash. Included in World state hashing so that
    /// any divergence in RNG progression surfaces immediately in OOS detection.
    /// </summary>
    public void HashInto(Fnv1a64 hash)
    {
        hash.Mix(_s0);
        hash.Mix(_s1);
        hash.Mix(_s2);
        hash.Mix(_s3);
    }
}
