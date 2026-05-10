using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;
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

    // TODO(, M1): once entities and movement exist, add:
    // [Fact] public void RandomMoveCommands_ReplayedTwice_ProduceIdenticalHashes()
    //
    // TODO(, M3): once gathering works, add:
    // [Fact] public void GatheringScenario_ReplayedTwice_ProduceIdenticalHashes()
    //
    // TODO(, M4): once combat works, add:
    // [Fact] public void CombatScenario_ReplayedTwice_ProduceIdenticalHashes()

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
