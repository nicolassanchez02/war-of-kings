namespace WarOfKings.Simulation.Core;

/// <summary>
/// Streaming FNV-1a 64-bit hash. Used to fingerprint deterministic simulation state
/// for OOS detection and the determinism test in CI.
///
/// FNV-1a is platform-independent (only integer ops), allocation-free, and has no
/// dependencies. xxHash64 would be faster but pulls a package; FNV-1a is sufficient
/// for our hash sizes and rates.
///
/// Reference: https://en.wikipedia.org/wiki/Fowler%E2%80%93Noll%E2%80%93Vo_hash_function
/// </summary>
public sealed class Fnv1a64
{
    private const ulong Offset = 0xCBF29CE484222325UL;
    private const ulong Prime  = 0x00000100000001B3UL;

    private ulong _hash = Offset;

    public ulong Result => _hash;

    public void MixByte(byte b)
    {
        _hash ^= b;
        _hash *= Prime;
    }

    public void Mix(ulong v)
    {
        ulong h = _hash;
        for (int i = 0; i < 8; i++)
        {
            h ^= (byte)(v >> (i * 8));
            h *= Prime;
        }
        _hash = h;
    }

    public void Mix(long v) => Mix((ulong)v);
    public void Mix(uint v) => Mix((ulong)v);
    public void Mix(int v) => Mix((ulong)(uint)v);
}
