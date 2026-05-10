# Determinism Reference

Determinism is the load-bearing technical requirement of this project. Lockstep multiplayer, replays, AI debugging, and reproducible bug reports all depend on it. This document is the detailed reference.

## The contract

Given the same starting world state, the same RNG seed, and the same ordered sequence of commands, the simulation must produce byte-identical state on every tick, on every machine, forever.

## Forbidden in simulation code

| Construct | Why | Use instead |
|-----------|-----|-------------|
| `float`, `double` | IEEE 754 ops differ across CPUs/compilers/optimization levels | `Fixed64` |
| `System.Random` | Implementation differs across .NET versions | `DeterministicRng` |
| `Math.Sin`, `Cos`, `Sqrt`, etc | Platform-dependent | `FixedMath` |
| `DateTime`, `Stopwatch` | Wall clock | `currentTick` |
| `Environment.TickCount` | Wall clock | `currentTick` |
| `Dictionary` / `HashSet` foreach | Iteration order undefined | `SortedDictionary`, sort first |
| `Parallel.*`, `Task.Run` in sim | Nondeterministic scheduling | Synchronous loops |
| `async` / `await` in sim | Continuation scheduling | Synchronous code |
| Reference equality on value-like data | Allocation-order dependent | Value equality, IDs |
| `GetHashCode()` on reference types | Address-based | Compute your own hash |
| `Guid.NewGuid()` | Pulls from OS entropy | Sequential `EntityId` |

## Fixed-point math

`Fixed64` is a 64-bit signed fixed-point number with 16 fractional bits. Range roughly +/- 140 trillion, precision about 0.000015.

```csharp
Fixed64 a = Fixed64.FromInt(5);
Fixed64 b = Fixed64.FromFraction(1, 3);  // 1/3
Fixed64 c = a * b;                        // 1.666...
int i = c.ToInt();                        // 1 (truncated)
```

Multiplication and division use 128-bit intermediate to avoid overflow. See `Fixed64.cs` for details.

`FixedMath` provides:
- `Sqrt(x)` - Newton's method, iterates to fixed precision
- `Sin(x)`, `Cos(x)` - Taylor series with normalization to [-pi, pi]
- `Atan2(y, x)` - lookup table + interpolation
- `Distance(a, b)` - Sqrt(dx*dx + dy*dy)

All return `Fixed64`. All are deterministic across platforms.

## The RNG

```csharp
public sealed class DeterministicRng {
    // xoshiro256** or PCG. Implementation choice TBD,
    // but it's a pure function of seed and call count.

    public DeterministicRng(ulong seed);
    public uint NextUInt();
    public int NextIntRange(int minInclusive, int maxExclusive);
    public Fixed64 NextFixed01();  // [0, 1)
}
```

One RNG instance per `World`. Seeded at match start from the match seed (which is part of the initial state).

If you need "random" behavior in multiple subsystems and don't want them to interfere with each other's call counts, give each subsystem its own RNG seeded from a derived hash of the master seed. Don't share.

## Entity processing order

Always process entities in ascending `EntityId` order. The factory hands out IDs sequentially. Storage should preserve insertion order, or you should sort at iteration time.

```csharp
// Good
foreach (var unit in world.Units.OrderedById()) { ... }

// Bad
foreach (var unit in world.UnitsDictionary.Values) { ... }
```

A LINQ `OrderBy` is fine, but cache the sorted list if you iterate it multiple times per tick.

## State hashing

After every tick, the simulation can produce a 64-bit hash of all state. This is what we compare across clients for OOS detection and across replay runs for determinism tests.

```csharp
public ulong ComputeStateHash() {
    var h = new XxHash64();
    h.Mix(currentTick);
    foreach (var entity in EntitiesOrderedById()) {
        entity.HashInto(h);
    }
    foreach (var player in PlayersOrderedById()) {
        player.HashInto(h);
    }
    return h.Result;
}
```

Every entity has a `HashInto(XxHash64)` method. When you add a new field to an entity, you must add it to `HashInto` too. The CI determinism test catches forgetting this.

## Determinism test

Lives in `tests/Determinism/`. Conceptually:

```csharp
[Fact]
public void RandomInputs_ReplayedTwice_ProduceIdenticalHashes() {
    var seed = 12345UL;
    var commandLog = GenerateRandomCommandLog(seed, ticks: 1000);

    var hashesA = RunSimulation(seed, commandLog);
    var hashesB = RunSimulation(seed, commandLog);

    Assert.Equal(hashesA, hashesB);
}
```

Run this on every PR. If it fails, do not merge. Find the cause.

We also have a cross-platform variant that the CI matrix runs on Windows, Linux, and macOS. If the hashes diverge across platforms, that's the most serious class of bug in this codebase. Stop everything and fix it.

## Common mistakes

**Iterating a Dictionary directly.** The values come out in insertion order in modern .NET but this is *not* guaranteed by the spec and could change. Use a list or sort.

**Hashing a `string` with `string.GetHashCode()`.** Randomized per process in .NET. Use a stable hash function like xxHash on the bytes.

**`Math.Sqrt` for distance calculations.** Pulls in floats. Use `FixedMath.Distance`. If you don't need exact distance, compare squared distances and skip the sqrt entirely.

**Pathfinding that uses heap tie-breaking by node reference.** Address-dependent. Tie-break by node ID.

**`DateTime.Now.Ticks` as a "fast counter."** Wall clock. Use `currentTick`.

**Initializing a field once and forgetting to hash it.** It's part of state. Hash it.

**Mutable static state.** Singleton caches in sim code are a determinism timebomb. No statics in `Simulation/` except `readonly` constants.

## When you absolutely need non-determinism

Sometimes you genuinely do. Examples: cosmetic particle effects, ambient sound choices, decorative animations.

That's fine, **as long as it lives in the renderer, not the simulation.** The renderer can use floats, `System.Random`, Godot's RNG, whatever. It doesn't matter because the renderer doesn't drive game state.

The rule: if removing it would change who wins the match, it belongs in the simulation and must be deterministic. Otherwise it can be wherever.
