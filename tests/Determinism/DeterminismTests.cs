using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Entities;
using WarOfKings.Simulation.Pathfinding;
using Xunit;

namespace WarOfKings.Determinism.Tests;

/// <summary>
/// The most important test in the codebase. It runs the simulation twice with identical
/// inputs and asserts identical resulting state hashes. If this fails, multiplayer and
/// replays are broken.
///
/// Add new scenarios here whenever a system is implemented:
///   - Movement determinism (M1) - DONE
///   - Pathing determinism (M1) - DONE
///   - Gathering determinism (M3)
///   - Combat determinism (M4)
///   - AI determinism (M7)
///   - Full match determinism (M8)
/// </summary>
public class DeterminismTests
{
    [Fact]
    public void EmptyWorld_StepsDeterministically()
    {
        var seed = 12345UL;
        var hashesA = RunWorld(seed, ticks: 100, commands: new List<Command>());
        var hashesB = RunWorld(seed, ticks: 100, commands: new List<Command>());

        Assert.Equal(hashesA, hashesB);
    }

    // M1 determinism test. De-skipped at the close of M1. The test asserts two things:
    //   1. Two replays of the same seeded random-input log produce identical hash sequences.
    //   2. The hash sequence is non-trivial — unit positions actually changed.
    [Fact]
    public void RandomMoveCommands_ReplayedTwice_ProduceIdenticalHashes()
    {
        const ulong seed = 0xD15EA5EDUL;
        const int ticks = 1000;
        const int numUnits = 4;

        var hashesA = RunRandomScenario(seed, ticks, numUnits);
        var hashesB = RunRandomScenario(seed, ticks, numUnits);

        Assert.Equal(hashesA, hashesB);

        // Sanity: the world actually progressed.
        var world = new World(seed);
        SpawnPattern(world, numUnits);
        var initialPositions = SnapshotPositions(world);
        StepWithRandomCommands(world, seed, ticks, numUnits);
        var finalPositions = SnapshotPositions(world);
        Assert.NotEqual(initialPositions, finalPositions);
    }

    /// <summary>
    /// Stress test: 100 units of mixed ownership, each issued a new MoveCommand to a random
    /// passable tile every 100 ticks. Two replays must produce identical hash sequences over
    /// 500 ticks. Catches non-determinism in path generation, occupancy updates, and any
    /// stale state inside MovementSystem's reusable A* pathfinder.
    /// </summary>
    [Fact]
    public void HundredUnitsPathing_ReplayedTwice_ProduceIdenticalHashes()
    {
        const ulong seed = 0xBABE1235UL;
        const int ticks = 500;
        const int numUnits = 100;

        var hashesA = RunRandomScenario(seed, ticks, numUnits);
        var hashesB = RunRandomScenario(seed, ticks, numUnits);

        Assert.Equal(hashesA, hashesB);
    }

    /// <summary>
    /// Narrow corridor contention: two units walk toward each other on a single-tile-wide
    /// passage. Whichever wins the corridor must do so the same way across replays — this
    /// stresses the deterministic order of MovementSystem's collision/wait/repath logic.
    /// </summary>
    [Fact]
    public void NarrowCorridorContention_ReplayedTwice_ProduceIdenticalHashes()
    {
        const ulong seed = 0xC07F1CC7UL;
        const int ticks = 600;

        var hashesA = RunCorridorScenario(seed, ticks);
        var hashesB = RunCorridorScenario(seed, ticks);
        Assert.Equal(hashesA, hashesB);
    }

    /// <summary>
    /// M3 gathering scenario: spawn a Town Hall, a tree cluster, two villagers, and issue a
    /// gather command. Two replays of the full gather/deposit/return cycle must hash-match.
    /// Catches non-determinism in resource state, carry transfer, drop-off selection, and the
    /// "find another same-kind resource" search.
    /// </summary>
    [Fact]
    public void GatheringScenario_ReplayedTwice_ProduceIdenticalHashes()
    {
        const ulong seed = 0xF00DBABEUL;
        const int ticks = 2000;

        var hashesA = RunGatherScenario(seed, ticks);
        var hashesB = RunGatherScenario(seed, ticks);
        Assert.Equal(hashesA, hashesB);

        // Sanity: a non-trivial amount of wood ended up in P1's stockpile.
        var verify = new World(seed);
        SetupGatherScenario(verify, out var villager, out var tcId, out var treeIds);
        var gather = new GatherCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Gatherers = new[] { villager.Id },
            ResourceNode = treeIds[0],
        };
        var empty = new List<Command>();
        for (int t = 0; t < ticks; t++) verify.Step(t == 0 ? new List<Command> { gather } : empty);
        Assert.True(verify.GetPlayer(PlayerId.Player1).Wood.ToInt() > 0,
            "Expected the villager to have deposited wood at least once over 2000 ticks.");
    }

    /// <summary>
    /// M4 combat scenario: two militia, mutual AttackCommands. Two replays of the entire
    /// fight (pursuit + attacks + at least one death) must produce identical hash sequences.
    /// </summary>
    [Fact]
    public void CombatScenario_ReplayedTwice_ProduceIdenticalHashes()
    {
        const ulong seed = 0xC0FFEEFACEUL;
        const int ticks = 500;

        var hashesA = RunCombatScenario(seed, ticks);
        var hashesB = RunCombatScenario(seed, ticks);
        Assert.Equal(hashesA, hashesB);
    }

    private static List<ulong> RunCombatScenario(ulong seed, int ticks)
    {
        var world = new World(seed);
        for (int y = 10; y < 30; y++)
            for (int x = 10; x < 30; x++)
                world.Map.SetTerrain(x, y, Terrain.Plain);

        var a = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(15, 15));
        a.UnitTypeId = 2; a.HpCurrent = Fixed64.FromInt(40); a.HpMax = Fixed64.FromInt(40);
        var b = world.CreateUnit(PlayerId.Player2, FixedVector2.FromInts(20, 15));
        b.UnitTypeId = 2; b.HpCurrent = Fixed64.FromInt(40); b.HpMax = Fixed64.FromInt(40);

        var commands = new List<Command>
        {
            new AttackCommand { ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1, Attackers = new[] { a.Id }, Target = b.Id },
            new AttackCommand { ExecuteAtTick = 0, Player = PlayerId.Player2, Sequence = 1, Attackers = new[] { b.Id }, Target = a.Id },
        };

        var hashes = new List<ulong>(ticks + 1) { world.ComputeStateHash() };
        var empty = new List<Command>();
        for (int t = 0; t < ticks; t++)
        {
            world.Step(t == 0 ? commands : empty);
            hashes.Add(world.ComputeStateHash());
        }
        return hashes;
    }

    /// <summary>
    /// Re-pathing determinism: a unit walks across the map, and at a fixed tick a wall is
    /// dropped onto its current path. The unit must re-path; two replays must produce the
    /// same path choice and the same resulting hashes. Wall placement uses SetTerrain
    /// directly (this is sim-internal — there is no BuildCommand yet) to mimic what M3
    /// construction will do when it lands.
    /// </summary>
    [Fact]
    public void PathInvalidatedMidWalk_ReplayedTwice_ProduceIdenticalHashes()
    {
        const ulong seed = 0xBADF00DBADF00DUL;
        const int ticks = 800;

        var hashesA = RunRepathScenario(seed, ticks);
        var hashesB = RunRepathScenario(seed, ticks);
        Assert.Equal(hashesA, hashesB);
    }

    // --- Scenario helpers ---

    private static List<ulong> RunRandomScenario(ulong seed, int ticks, int numUnits)
    {
        var world = new World(seed);
        SpawnPattern(world, numUnits);
        return StepWithRandomCommands(world, seed, ticks, numUnits);
    }

    private static void SpawnPattern(World world, int numUnits)
    {
        // Spawn on the same passable rows to avoid landing on Mountain/Water in the random grid.
        // The procedural grid has ~12% non-passable tiles; we walk along passable spots.
        int spawned = 0;
        int x = 5, y = 5;
        while (spawned < numUnits)
        {
            if (world.Map.IsPassable(x, y))
            {
                var owner = (spawned % 2 == 0) ? PlayerId.Player1 : PlayerId.Player2;
                world.CreateUnit(owner, FixedVector2.FromInts(x, y));
                spawned++;
            }
            x += 2;
            if (x >= Grid.Width - 5) { x = 5; y += 2; }
        }
    }

    private static List<ulong> StepWithRandomCommands(World world, ulong seed, int ticks, int numUnits)
    {
        var inputRng = new DeterministicRng(seed ^ 0xC0FFEEC0FFEEUL);
        var hashes = new List<ulong>(ticks + 1) { world.ComputeStateHash() };

        for (int t = 0; t < ticks; t++)
        {
            var commands = new List<Command>();

            if (t % 100 == 0)
            {
                for (long id = 1; id <= numUnits; id++)
                {
                    int tx, ty;
                    do
                    {
                        tx = inputRng.NextIntRange(0, Grid.Width);
                        ty = inputRng.NextIntRange(0, Grid.Height);
                    } while (!world.Map.IsPassable(tx, ty));

                    commands.Add(new MoveCommand
                    {
                        ExecuteAtTick = t,
                        Player = (id % 2 == 1) ? PlayerId.Player1 : PlayerId.Player2,
                        Sequence = (uint)((t * 7919) + id),
                        Units = new[] { new EntityId(id) },
                        Target = FixedVector2.FromInts(tx, ty),
                    });
                }
            }

            world.Step(commands);
            hashes.Add(world.ComputeStateHash());
        }

        return hashes;
    }

    private static List<ulong> RunGatherScenario(ulong seed, int ticks)
    {
        var world = new World(seed);
        SetupGatherScenario(world, out var villager, out var tcId, out var treeIds);
        var gather = new GatherCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Gatherers = new[] { villager.Id },
            ResourceNode = treeIds[0],
        };

        var hashes = new List<ulong>(ticks + 1) { world.ComputeStateHash() };
        var initial = new List<Command> { gather };
        var empty = new List<Command>();
        for (int t = 0; t < ticks; t++)
        {
            world.Step(t == 0 ? initial : empty);
            hashes.Add(world.ComputeStateHash());
        }
        return hashes;
    }

    private static void SetupGatherScenario(World world, out Unit villager, out EntityId tcId, out List<EntityId> treeIds)
    {
        // Flat arena around the working area.
        for (int y = 10; y < 30; y++)
            for (int x = 10; x < 30; x++)
                world.Map.SetTerrain(x, y, Terrain.Plain);

        world.GetPlayer(PlayerId.Player1).Wood = Fixed64.Zero;
        var tc = world.CreateBuilding(BuildingTypeId.TownHall, PlayerId.Player1, tileX: 15, tileY: 15, footprintW: 3, footprintH: 3, hpMax: 600);
        tcId = tc.Id;
        treeIds = new List<EntityId>();
        for (int i = 0; i < 3; i++)
            treeIds.Add(world.CreateTree(20 + i, 20).Id);
        villager = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(18, 18));
    }

    private static List<ulong> RunCorridorScenario(ulong seed, int ticks)
    {
        var world = new World(seed);
        // Carve a single-tile-wide passable corridor at y=10 between x=5 and x=20 by walling
        // off y=9 and y=11 in that range. Force the start tiles to be passable too.
        for (int x = 4; x <= 21; x++)
        {
            world.Map.SetTerrain(x, 9, Terrain.Mountain);
            world.Map.SetTerrain(x, 11, Terrain.Mountain);
            world.Map.SetTerrain(x, 10, Terrain.Plain);
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

        var hashes = new List<ulong>(ticks + 1) { world.ComputeStateHash() };
        var initial = new List<Command> { moveA, moveB };
        var empty = new List<Command>();
        for (int t = 0; t < ticks; t++)
        {
            world.Step(t == 0 ? initial : empty);
            hashes.Add(world.ComputeStateHash());
        }
        return hashes;
    }

    private static List<ulong> RunRepathScenario(ulong seed, int ticks)
    {
        var world = new World(seed);
        // Clear a corridor along y=50 to remove map noise; we want a clean re-path scenario.
        for (int x = 5; x <= 150; x++)
            for (int y = 48; y <= 52; y++)
                world.Map.SetTerrain(x, y, Terrain.Plain);

        var unit = world.CreateUnit(PlayerId.Player1, FixedVector2.FromInts(10, 50));
        var move = new MoveCommand
        {
            ExecuteAtTick = 0, Player = PlayerId.Player1, Sequence = 1,
            Units = new[] { unit.Id }, Target = FixedVector2.FromInts(140, 50),
        };

        var hashes = new List<ulong>(ticks + 1) { world.ComputeStateHash() };
        var initial = new List<Command> { move };
        var empty = new List<Command>();
        for (int t = 0; t < ticks; t++)
        {
            // At tick 200, drop a 3-tile vertical wall in front of the unit. This invalidates
            // its current path. The unit must detect the block, wait, repath, and continue.
            if (t == 200)
            {
                int wallX = 80;
                world.Map.SetTerrain(wallX, 49, Terrain.Mountain);
                world.Map.SetTerrain(wallX, 50, Terrain.Mountain);
                world.Map.SetTerrain(wallX, 51, Terrain.Mountain);
            }
            world.Step(t == 0 ? initial : empty);
            hashes.Add(world.ComputeStateHash());
        }
        return hashes;
    }

    private static List<FixedVector2> SnapshotPositions(World world)
    {
        var positions = new List<FixedVector2>();
        foreach (var u in world.UnitsOrderedById()) positions.Add(u.Position);
        return positions;
    }

    private static List<ulong> RunWorld(ulong seed, int ticks, IReadOnlyList<Command> commands)
    {
        var world = new World(seed);
        var hashes = new List<ulong>(ticks + 1);
        hashes.Add(world.ComputeStateHash());

        var byTick = new Dictionary<long, List<Command>>();
        foreach (var c in commands)
        {
            if (!byTick.TryGetValue(c.ExecuteAtTick, out var list))
            {
                list = new List<Command>();
                byTick[c.ExecuteAtTick] = list;
            }
            list.Add(c);
        }

        for (int t = 0; t < ticks; t++)
        {
            var forThisTick = byTick.TryGetValue(t, out var list) ? list : new List<Command>();
            world.Step(forThisTick);
            hashes.Add(world.ComputeStateHash());
        }

        return hashes;
    }
}
