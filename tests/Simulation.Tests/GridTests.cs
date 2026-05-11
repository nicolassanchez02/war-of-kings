using WarOfKings.Simulation;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Pathfinding;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class GridTests
{
    [Fact]
    public void Generate_SameSeed_SameContents()
    {
        var a = Grid.Generate(123UL);
        var b = Grid.Generate(123UL);
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                Assert.Equal(a.GetTerrain(x, y), b.GetTerrain(x, y));
    }

    [Fact]
    public void Generate_DifferentSeeds_DifferentContents()
    {
        var a = Grid.Generate(1UL);
        var b = Grid.Generate(2UL);
        int diffs = 0;
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                if (a.GetTerrain(x, y) != b.GetTerrain(x, y)) diffs++;
        Assert.True(diffs > Grid.Width * Grid.Height / 4,
            $"Expected substantial divergence between seeds, only {diffs} tiles differ.");
    }

    [Fact]
    public void IsPassable_RespectsTerrainRules()
    {
        var g = Grid.Generate(0UL);
        g.SetTerrain(0, 0, Terrain.Plain);
        g.SetTerrain(1, 0, Terrain.Forest);
        g.SetTerrain(2, 0, Terrain.Mountain);
        g.SetTerrain(3, 0, Terrain.Water);
        g.SetTerrain(4, 0, Terrain.Gold);

        Assert.True(g.IsPassable(0, 0));   // Plain
        Assert.True(g.IsPassable(1, 0));   // Forest (walk through, harvest later)
        Assert.False(g.IsPassable(2, 0));  // Mountain
        Assert.False(g.IsPassable(3, 0));  // Water
        Assert.True(g.IsPassable(4, 0));   // Gold (walk onto to mine)
    }

    [Fact]
    public void HashInto_SameGrid_SameHash()
    {
        var a = Grid.Generate(7UL);
        var b = Grid.Generate(7UL);
        var ha = new Fnv1a64(); a.HashInto(ha);
        var hb = new Fnv1a64(); b.HashInto(hb);
        Assert.Equal(ha.Result, hb.Result);
    }

    [Fact]
    public void HashInto_GridIncluded_InWorldHash()
    {
        // Changing a tile must change World.ComputeStateHash. If this fails,
        // the grid isn't actually contributing to state hashing — a multiplayer
        // OOS detection blindspot.
        var w = new World(42UL);
        var before = w.ComputeStateHash();
        var originalTerrain = w.Map.GetTerrain(50, 50);
        var newTerrain = originalTerrain == Terrain.Plain ? Terrain.Mountain : Terrain.Plain;
        w.Map.SetTerrain(50, 50, newTerrain);
        var after = w.ComputeStateHash();
        Assert.NotEqual(before, after);
    }

    [Fact]
    public void World_SameSeed_SameMap()
    {
        var a = new World(99UL);
        var b = new World(99UL);
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                Assert.Equal(a.Map.GetTerrain(x, y), b.Map.GetTerrain(x, y));
    }
}
