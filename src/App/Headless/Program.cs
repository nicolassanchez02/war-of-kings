using System;
using System.Collections.Generic;
using WarOfKings.Simulation;
using WarOfKings.Simulation.Commands;
using WarOfKings.Simulation.Core;

namespace WarOfKings.App.Headless;

// Headless harness for the simulation. Runs the World for a configurable number of ticks
// and prints the state hash at intervals. The point is to confirm by eye that the sim
// advances deterministically: the same --seed should always produce the same hash sequence.
//
// Usage:
//   dotnet run --project src/App/Headless                  -> default: seed 0, 100 ticks
//   dotnet run --project src/App/Headless -- --seed 42     -> custom seed
//   dotnet run --project src/App/Headless -- --ticks 1000  -> custom tick count
//   dotnet run --project src/App/Headless -- --every 50    -> print hash every N ticks
//   dotnet run --project src/App/Headless -- --twice       -> run the sim twice and diff
//   dotnet run --project src/App/Headless -- --spawn 3     -> spawn N starter units and dump them
public static class Program
{
    public static int Main(string[] args)
    {
        var opts = ParseArgs(args);
        if (opts is null) { PrintUsage(); return 2; }

        Console.WriteLine($"War of Kings - headless simulation runner");
        Console.WriteLine($"seed={opts.Seed}  ticks={opts.Ticks}  every={opts.Every}  twice={opts.Twice}  spawn={opts.Spawn}");
        Console.WriteLine(new string('-', 60));

        var hashesA = RunOnce(opts);

        if (opts.Twice)
        {
            Console.WriteLine();
            Console.WriteLine("Replaying with the same seed...");
            var hashesB = RunOnce(opts);

            Console.WriteLine();
            if (HashesEqual(hashesA, hashesB))
            {
                Console.WriteLine("DETERMINISTIC: both runs produced identical hash sequences.");
                return 0;
            }

            Console.WriteLine("NONDETERMINISTIC: hash sequences diverged. This is a bug.");
            for (int i = 0; i < hashesA.Count; i++)
            {
                if (hashesA[i] != hashesB[i])
                {
                    Console.WriteLine($"  first divergence at index {i}: A={hashesA[i]:X16} B={hashesB[i]:X16}");
                    break;
                }
            }
            return 1;
        }

        return 0;
    }

    private static List<ulong> RunOnce(Options opts)
    {
        var world = new World(opts.Seed);
        var commands = new List<Command>();
        var hashes = new List<ulong>(opts.Ticks + 1);

        // Optional starter units, alternating between Player1 and Player2 on a small spread.
        for (int i = 0; i < opts.Spawn; i++)
        {
            var owner = (i % 2 == 0) ? PlayerId.Player1 : PlayerId.Player2;
            var pos = FixedVector2.FromInts(10 * i, 5 * i);
            var u = world.CreateUnit(owner, pos);
            Console.WriteLine($"  spawned {u.Id} for {owner} at {u.Position}");
        }
        if (opts.Spawn > 0) Console.WriteLine();

        var initial = world.ComputeStateHash();
        hashes.Add(initial);
        Console.WriteLine($"  tick {world.CurrentTick,6}: {initial:X16}  (initial)");

        for (int t = 0; t < opts.Ticks; t++)
        {
            world.Step(commands);
            var h = world.ComputeStateHash();
            hashes.Add(h);

            if ((t + 1) % opts.Every == 0 || t == opts.Ticks - 1)
            {
                Console.WriteLine($"  tick {world.CurrentTick,6}: {h:X16}");
            }
        }

        // Final unit dump (positions are static until M1 movement lands, but the format is useful).
        if (opts.Spawn > 0)
        {
            Console.WriteLine();
            Console.WriteLine("  final unit state:");
            foreach (var u in world.UnitsOrderedById())
            {
                Console.WriteLine($"    {u.Id} owner={u.Owner} pos={u.Position} hp={u.HpCurrent}/{u.HpMax}");
            }
        }

        return hashes;
    }

    private static bool HashesEqual(List<ulong> a, List<ulong> b)
    {
        if (a.Count != b.Count) return false;
        for (int i = 0; i < a.Count; i++) if (a[i] != b[i]) return false;
        return true;
    }

    private static Options? ParseArgs(string[] args)
    {
        var opts = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--seed":
                    if (++i >= args.Length || !ulong.TryParse(args[i], out var seed)) return null;
                    opts.Seed = seed;
                    break;
                case "--ticks":
                    if (++i >= args.Length || !int.TryParse(args[i], out var ticks) || ticks < 0) return null;
                    opts.Ticks = ticks;
                    break;
                case "--every":
                    if (++i >= args.Length || !int.TryParse(args[i], out var every) || every < 1) return null;
                    opts.Every = every;
                    break;
                case "--twice":
                    opts.Twice = true;
                    break;
                case "--spawn":
                    if (++i >= args.Length || !int.TryParse(args[i], out var spawn) || spawn < 0) return null;
                    opts.Spawn = spawn;
                    break;
                case "--help":
                case "-h":
                    return null;
                default:
                    Console.Error.WriteLine($"Unknown argument: {args[i]}");
                    return null;
            }
        }
        return opts;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("Usage: dotnet run --project src/App/Headless -- [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --seed <uint>      RNG seed (default 0)");
        Console.WriteLine("  --ticks <int>      number of ticks to run (default 100)");
        Console.WriteLine("  --every <int>      print hash every N ticks (default 10)");
        Console.WriteLine("  --twice            run twice and assert hash sequences match");
        Console.WriteLine("  --spawn <int>      spawn N starter units before stepping (default 0)");
        Console.WriteLine("  -h, --help         show this");
    }

    private sealed class Options
    {
        public ulong Seed { get; set; } = 0;
        public int Ticks { get; set; } = 100;
        public int Every { get; set; } = 10;
        public bool Twice { get; set; } = false;
        public int Spawn { get; set; } = 0;
    }
}
