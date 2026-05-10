using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class WorldHashTests
{
    private static readonly IReadOnlyList<Command> NoCommands = new List<Command>();

    [Fact]
    public void EmptyWorlds_SameSeed_SameHash()
    {
        var a = new World(42);
        var b = new World(42);
        Assert.Equal(a.ComputeStateHash(), b.ComputeStateHash());
    }

    [Fact]
    public void EmptyWorlds_DifferentSeeds_DifferentHash()
    {
        var a = new World(1);
        var b = new World(2);
        Assert.NotEqual(a.ComputeStateHash(), b.ComputeStateHash());
    }

    [Fact]
    public void Stepping_ChangesHash()
    {
        var w = new World(7);
        var before = w.ComputeStateHash();
        w.Step(NoCommands);
        var after = w.ComputeStateHash();
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void Hash_IsNonZero()
    {
        // FNV-1a with the standard offset never returns zero on the first call,
        // but document the invariant: an empty world has a definite, non-zero hash.
        var w = new World(0);
        Assert.NotEqual(0UL, w.ComputeStateHash());
    }

    [Fact]
    public void AllocateEntityId_AdvancesHash()
    {
        // _nextEntityId is part of the hash, so allocating an ID without registering anything
        // should still change the world's fingerprint.
        var w = new World(0);
        var before = w.ComputeStateHash();
        _ = w.AllocateEntityId();
        var after = w.ComputeStateHash();
        Assert.NotEqual(before, after);
    }
}
