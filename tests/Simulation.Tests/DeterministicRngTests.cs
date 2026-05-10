using System.Collections.Generic;
using WarOfKings.Simulation.Core;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class DeterministicRngTests
{
    [Fact]
    public void SameSeed_ProducesSameSequence()
    {
        var a = new DeterministicRng(42);
        var b = new DeterministicRng(42);

        for (int i = 0; i < 1000; i++)
        {
            Assert.Equal(a.NextULong(), b.NextULong());
        }
    }

    [Fact]
    public void DifferentSeeds_ProduceDifferentSequences()
    {
        var a = new DeterministicRng(1);
        var b = new DeterministicRng(2);

        // Very small chance of collision; should never happen in practice for first value.
        Assert.NotEqual(a.NextULong(), b.NextULong());
    }

    [Fact]
    public void NextIntRange_StaysInRange()
    {
        var rng = new DeterministicRng(123);
        for (int i = 0; i < 10000; i++)
        {
            var v = rng.NextIntRange(5, 10);
            Assert.InRange(v, 5, 9);
        }
    }

    [Fact]
    public void NextFixed01_StaysInRange()
    {
        var rng = new DeterministicRng(456);
        for (int i = 0; i < 10000; i++)
        {
            var v = rng.NextFixed01();
            Assert.True(v >= Fixed64.Zero);
            Assert.True(v < Fixed64.OneValue);
        }
    }

    [Fact]
    public void KnownSequence_IsStable()
    {
        // CANONICAL REFERENCE: the first 5 outputs of seed=0.
        // Pinned 2026-05-10 against xoshiro256** seeded via SplitMix64.
        // Changing any value here breaks every replay in the wild.
        // Treat that as a deliberate, versioned event.
        var rng = new DeterministicRng(0);
        var actual = new ulong[5];
        for (int i = 0; i < 5; i++) actual[i] = rng.NextULong();

        var expected = new ulong[]
        {
            0x99EC5F36CB75F2B4UL,
            0xBF6E1F784956452AUL,
            0x1A5F849D4933E6E0UL,
            0x6AA594F1262D2D2CUL,
            0xBBA5AD4A1F842E59UL,
        };
        Assert.Equal(expected, actual);
    }
}
