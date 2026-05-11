using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
using WarOfKings.Simulation.Pathfinding;
using Xunit;

namespace WarOfKings.Determinism.Tests;

/// <summary>
/// The most important test in the codebase. It runs the simulation twice with identical
/// inputs and asserts identical resulting state hashes. If this fails, multiplayer and
/// replays are broken.
///
/// Add new scenarios here whenever a system is implemented:
///   - Movement determinism (M1)
///   - Pathing determinism (M1)
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

    // M1 determinism test, written ahead of the movement system per the brief's
    // "test first" discipline. Skipped while the system is incomplete; CI stays
    // green during M1 work. Remove the Skip attribute as the last commit of M1
    // — that commit is the milestone gate.
    //
    // The test asserts two things:
    //   1. Two replays of the same seeded random-input log produce identical
    //      hash sequences (the canonical determinism contract).
    //   2. The hash sequence is non-trivial — hashes diverge over time as units
    //      actually move. Without this, the test passes vacuously even when no
    //      command is processed.
    [Fact(Skip = "Activate when MoveCommand processing + movement system land in M1.")]
    public void RandomMoveCommands_ReplayedTwice_ProduceIdenticalHashes()
    {
        const ulong seed = 0xD15EA5EDUL;
        const int ticks = 1000;
        const int numUnits = 4;

        var hashesA = RunRandomScenario(seed, ticks, numUnits);
        var hashesB = RunRandomScenario(seed, ticks, numUnits);

        Assert.Equal(hashesA, hashesB);

        // Sanity: the world actually progressed. If commands aren't being
        // applied, every tick's hash will differ only by CurrentTick mixing.
        // That mixing alone changes the hash — so the right sanity is that
        // unit positions changed. We check unit hashes by re-running and
        // capturing position state via the public unit accessor.
        var world = new World(seed);
        SpawnPattern(world, numUnits);
        var initialPositions = SnapshotPositions(world);
        StepWithRandomCommands(world, seed, ticks, numUnits);
        var finalPositions = SnapshotPositions(world);
        Assert.NotEqual(initialPositions, finalPositions);
    }

    private static List<ulong> RunRandomScenario(ulong seed, int ticks, int numUnits)
    {
        var world = new World(seed);
        SpawnPattern(world, numUnits);
        return StepWithRandomCommands(world, seed, ticks, numUnits);
    }

    private static void SpawnPattern(World world, int numUnits)
    {
        for (int i = 0; i < numUnits; i++)
        {
            var owner = (i % 2 == 0) ? PlayerId.Player1 : PlayerId.Player2;
            world.CreateUnit(owner, FixedVector2.FromInts(20 + i * 3, 20 + i * 3));
        }
    }

    private static List<ulong> StepWithRandomCommands(World world, ulong seed, int ticks, int numUnits)
    {
        // Dedicated RNG so the command stream is independent of the world's own draws.
        var inputRng = new DeterministicRng(seed ^ 0xC0FFEEC0FFEEUL);
        var hashes = new List<ulong>(ticks + 1) { world.ComputeStateHash() };

        for (int t = 0; t < ticks; t++)
        {
            var commands = new List<Command>();

            // Emit a MoveCommand every 25 ticks, targeting a random passable tile.
            if (t % 25 == 0)
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

    private static List<FixedVector2> SnapshotPositions(World world)
    {
        var positions = new List<FixedVector2>();
        foreach (var u in world.UnitsOrderedById()) positions.Add(u.Position);
        return positions;
    }

    // TODO(M3): GatheringScenario_ReplayedTwice_ProduceIdenticalHashes
    // TODO(M4): CombatScenario_ReplayedTwice_ProduceIdenticalHashes

    private static List<ulong> RunWorld(ulong seed, int ticks, IReadOnlyList<Command> commands)
    {
        var world = new World(seed);
        var hashes = new List<ulong>(ticks + 1);
        hashes.Add(world.ComputeStateHash());

        // Bucket commands by tick.
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
