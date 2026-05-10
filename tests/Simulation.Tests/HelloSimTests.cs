using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class HelloSimTests
{
    private static readonly IReadOnlyList<Command> NoCommands = new List<Command>();

    [Fact]
    public void World_WithOneUnit_Steps100Ticks_DeterministicHash()
    {
        // The M0 "hello sim" check: a World with one entity, stepped 100 ticks,
        // produces the same hash sequence on two independent runs.
        var hashesA = Run();
        var hashesB = Run();
        Assert.Equal(hashesA, hashesB);
        // Sanity: the final hash isn't zero or constant.
        Assert.NotEqual(0UL, hashesA[^1]);
        Assert.NotEqual(hashesA[0], hashesA[^1]);
    }

    private static List<ulong> Run()
    {
        var world = new World(0xC0FFEEUL);
        var unit = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 20));
        Assert.Equal(1, unit.Id.Value);

        var hashes = new List<ulong> { world.ComputeStateHash() };
        for (int i = 0; i < 100; i++)
        {
            world.Step(NoCommands);
            hashes.Add(world.ComputeStateHash());
        }
        return hashes;
    }

    [Fact]
    public void CreateUnit_AllocatesMonotonicIds()
    {
        var w = new World(0);
        var a = w.CreateUnit(PlayerId.Player1, FixedVector2.Zero);
        var b = w.CreateUnit(PlayerId.Player2, FixedVector2.Zero);
        var c = w.CreateUnit(PlayerId.Player1, FixedVector2.Zero);

        Assert.Equal(1, a.Id.Value);
        Assert.Equal(2, b.Id.Value);
        Assert.Equal(3, c.Id.Value);
    }

    [Fact]
    public void UnitsOrderedById_VisitsInIdOrder()
    {
        var w = new World(0);
        var a = w.CreateUnit(PlayerId.Player1, FixedVector2.Zero);
        var b = w.CreateUnit(PlayerId.Player2, FixedVector2.Zero);
        var c = w.CreateUnit(PlayerId.Player1, FixedVector2.Zero);

        var ids = new List<long>();
        foreach (var u in w.UnitsOrderedById()) ids.Add(u.Id.Value);
        Assert.Equal(new[] { a.Id.Value, b.Id.Value, c.Id.Value }, ids);
    }

    [Fact]
    public void Unit_FieldsChangeHash()
    {
        // Changing any hashed field on a Unit must change the world hash.
        var w = new World(0);
        var u = w.CreateUnit(PlayerId.Player1, FixedVector2.Zero);
        var baseline = w.ComputeStateHash();

        u.Position = FixedVector2.FromInts(1, 0);
        Assert.NotEqual(baseline, w.ComputeStateHash());

        u.Position = FixedVector2.Zero;
        u.HpCurrent = Fixed64.FromInt(1);
        Assert.NotEqual(baseline, w.ComputeStateHash());
    }
}
