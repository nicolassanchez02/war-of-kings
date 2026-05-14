using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;
using Xunit;

namespace WarOfKings.Simulation.Tests;

public class MovementSystemTests
{
    private static World OpenWorld(ulong seed = 1UL)
    {
        // Construct a world with a fully plain map so terrain doesn't interfere with movement tests.
        var w = new World(seed);
        for (int y = 0; y < Grid.Height; y++)
            for (int x = 0; x < Grid.Width; x++)
                w.Map.SetTerrain(x, y, Terrain.Plain);
        return w;
    }

    private static void RunTicks(World world, int ticks, List<Command>? commands = null)
    {
        var empty = new List<Command>();
        for (int i = 0; i < ticks; i++)
        {
            world.Step(commands ?? empty);
            commands = null; // commands fire only once, on tick 0
        }
    }

    [Fact]
    public void Move_command_makes_unit_walk_to_target()
    {
        var world = OpenWorld();
        var unit = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 10));

        var move = new MoveCommand
        {
            ExecuteAtTick = 0,
            Player = PlayerId.Player1,
            Sequence = 1,
            Units = new[] { unit.Id },
            Target = FixedVector2.FromInts(20, 10),
        };

        // 0.045 tiles per tick, 10 tile distance => ~225 ticks. Give it ample budget.
        RunTicks(world, 1000, new List<Command> { move });

        // Unit reached destination tile (occupancy on tile 20,10).
        Assert.Equal(20, unit.Position.X.ToInt());
        Assert.Equal(10, unit.Position.Y.ToInt());
        Assert.Equal(UnitState.Idle, unit.State);
        Assert.False(unit.HasPath);
    }

    [Fact]
    public void Re_issuing_move_cancels_current_path()
    {
        var world = OpenWorld();
        var unit = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 10));

        var first = new MoveCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Units = new[] { unit.Id }, Target = FixedVector2.FromInts(50, 10),
        };
        world.Step(new List<Command> { first });
        // Step a few times so the unit is moving.
        for (int i = 0; i < 30; i++) world.Step(new List<Command>());

        Assert.Equal(UnitState.Moving, unit.State);
        int destBefore = unit.DestinationTileIdx;

        // New command in a different direction.
        var second = new MoveCommand
        {
            ExecuteAtTick = world.CurrentTick, Player = PlayerId.Player1, Sequence = 2,
            Units = new[] { unit.Id }, Target = FixedVector2.FromInts(10, 50),
        };
        world.Step(new List<Command> { second });

        // After applying the new command, destination should change and path is now toward (10, 50).
        Assert.NotEqual(destBefore, unit.PendingDestinationIdx == -1 ? unit.DestinationTileIdx : unit.PendingDestinationIdx);
    }

    [Fact]
    public void Tile_occupancy_updates_as_unit_moves()
    {
        var world = OpenWorld();
        var unit = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 10));
        int startTile = unit.CurrentTileIdx;

        Assert.Equal(unit.Id, world.GetOccupant(startTile));

        var move = new MoveCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Units = new[] { unit.Id }, Target = FixedVector2.FromInts(15, 10),
        };
        RunTicks(world, 500, new List<Command> { move });

        // Final occupancy must be on the destination tile, and the original starting tile
        // must be free (assuming the unit moved away from it, which is guaranteed for the
        // 5-tile distance + 500-tick budget).
        int endTile = unit.CurrentTileIdx;
        Assert.NotEqual(startTile, endTile);
        Assert.Equal(unit.Id, world.GetOccupant(endTile));
        Assert.True(world.GetOccupant(startTile).IsNone);
    }

    [Fact]
    public void Two_units_into_narrow_corridor_dont_overlap()
    {
        // A 1-tile-wide horizontal corridor between two units. They issue moves toward each
        // other; whichever wins the corridor must do so, and the other must never enter the
        // same tile.
        var world = OpenWorld();
        // Walls on row 9 and row 11 leave row 10 as the corridor.
        for (int x = 5; x <= 20; x++)
        {
            world.Map.SetTerrain(x, 9, Terrain.Mountain);
            world.Map.SetTerrain(x, 11, Terrain.Mountain);
        }
        var a = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(6, 10));
        var b = world.CreateUnit(PlayerId.Player2, FixedVector2.FromInts(18, 10));

        var moveA = new MoveCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Units = new[] { a.Id }, Target = FixedVector2.FromInts(18, 10),
        };
        var moveB = new MoveCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player2, Sequence = 1,
            Units = new[] { b.Id }, Target = FixedVector2.FromInts(6, 10),
        };
        RunTicks(world, 1500, new List<Command> { moveA, moveB });

        // The invariant under test: at no point can their tile occupancy overlap. Run a quick
        // observable invariant — current tiles are different and the occupancy table is consistent.
        Assert.NotEqual(a.CurrentTileIdx, b.CurrentTileIdx);
        Assert.Equal(a.Id, world.GetOccupant(a.CurrentTileIdx));
        Assert.Equal(b.Id, world.GetOccupant(b.CurrentTileIdx));
    }

    [Fact]
    public void Unreachable_destination_leaves_unit_idle()
    {
        var world = OpenWorld();
        // Surround (50, 50) with mountains so it's unreachable, even from (10, 10).
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if (dx != 0 || dy != 0)
                    world.Map.SetTerrain(50 + dx, 50 + dy, Terrain.Mountain);

        var unit = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 10));
        var move = new MoveCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Units = new[] { unit.Id }, Target = FixedVector2.FromInts(50, 50),
        };
        RunTicks(world, 5, new List<Command> { move });

        Assert.Equal(UnitState.Idle, unit.State);
        Assert.Equal(-1, unit.DestinationTileIdx);
        Assert.False(unit.HasPath);
    }

    [Fact]
    public void Multi_unit_move_spreads_units_around_target()
    {
        var world = OpenWorld();
        var u1 = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 10));
        var u2 = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(11, 10));
        var u3 = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 11));
        var u4 = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(11, 11));

        var move = new MoveCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Units = new[] { u1.Id, u2.Id, u3.Id, u4.Id },
            Target = FixedVector2.FromInts(30, 30),
        };
        RunTicks(world, 1500, new List<Command> { move });

        // Final tiles must be distinct (no two units land on the same tile).
        var tiles = new HashSet<int> { u1.CurrentTileIdx, u2.CurrentTileIdx, u3.CurrentTileIdx, u4.CurrentTileIdx };
        Assert.Equal(4, tiles.Count);
    }
}
