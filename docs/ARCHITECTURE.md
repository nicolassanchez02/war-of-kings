# Architecture

This document is the contract for how War of Kings is built. Deviations need a good reason and should be discussed before merge.

## Core principle: simulation and presentation are separate

The most important architectural rule. Everything else follows from it.

```
+--------------------+       +--------------------+
|   Input layer      |       |   Renderer         |
|   (mouse, kbd,     |       |   (Godot nodes,    |
|    network, AI)    |       |    sprites, UI)    |
+----------+---------+       +---------+----------+
           |                           ^
           v                           |
+---------------------+    +-----------+----------+
|   Command queue     |    |   Simulation state   |
|   (deterministic    |--->|   (pure C#, no       |
|    ordered inputs)  |    |    Godot dependency) |
+---------------------+    +----------------------+
```

The **simulation** is a pure C# library. It has no `using Godot;` statements. It can be run headlessly, in tests, on a server, anywhere. It advances in discrete ticks at a fixed rate (20 Hz target).

The **renderer** reads simulation state and draws it. It can interpolate visually between ticks for smooth motion, but it never writes back to the simulation.

The **input layer** converts player intent (mouse clicks, AI decisions, network packets) into typed `Command` objects that go into a queue. Commands are applied at well-defined tick boundaries.

If you ever find yourself wanting to put a `Sprite2D` reference inside a `Unit`, stop. That's the contract breaking. The `Unit` lives in the simulation. The sprite lives in the renderer. They're connected by an ID.

## Determinism contract

These rules are non-negotiable. Breaking any one of them breaks multiplayer and replays.

1. **No `float` or `double` in the simulation.** Use `Fixed64` (our fixed-point type). Floats are fine in the renderer.

2. **No `System.Random` in the simulation.** Use the seeded `DeterministicRng` and pass the seed in the match start command.

3. **No iteration over unordered collections in simulation logic.** No `HashSet` foreach, no `Dictionary` foreach. Use `SortedDictionary`, `List`, or sort before iterating. `Dictionary` is fine for lookup, just not for ordered iteration.

4. **No DateTime, no Stopwatch, no real wall-clock time in simulation.** Use `currentTick` (a `long`).

5. **No platform-specific math.** Use our `FixedMath` library. Don't call `Math.Sin`, `Math.Cos`, `Math.Sqrt` directly. They differ across platforms and CPUs.

6. **Entity processing order must be stable.** Entities are processed in order of their `EntityId` (monotonically increasing `long`), always.

7. **All entity creation goes through a single factory** that assigns the next `EntityId`. Never `new Unit(...)` directly.

8. **Tick boundaries are sacrosanct.** A tick takes inputs, advances state, produces outputs. Nothing crosses a tick boundary except through the command queue and state snapshot.

A determinism test runs on every PR (see `tests/Determinism/`). It replays a recorded input log twice and asserts the resulting state hashes match. If it fails, the PR doesn't merge.

## Project structure

```
src/
├── Simulation/           Pure C# class library, no Godot deps
│   ├── Core/
│   │   ├── Fixed64.cs           Fixed-point number type
│   │   ├── FixedVector2.cs      2D vector using Fixed64
│   │   ├── FixedMath.cs         Deterministic sin/cos/sqrt/etc
│   │   ├── DeterministicRng.cs  Seeded RNG
│   │   ├── EntityId.cs          Strongly-typed ID
│   │   └── World.cs             The root simulation state
│   ├── Entities/
│   │   ├── Entity.cs            Base entity
│   │   ├── Unit.cs              Mobile entities
│   │   ├── Building.cs          Static entities
│   │   └── Resource.cs          Trees, mines, berry bushes
│   ├── Commands/
│   │   ├── Command.cs           Base command
│   │   ├── MoveCommand.cs
│   │   ├── GatherCommand.cs
│   │   ├── BuildCommand.cs
│   │   ├── TrainCommand.cs
│   │   └── AttackCommand.cs
│   ├── Systems/
│   │   ├── MovementSystem.cs
│   │   ├── PathfindingSystem.cs
│   │   ├── GatheringSystem.cs
│   │   ├── CombatSystem.cs
│   │   ├── BuildingSystem.cs
│   │   ├── ProductionSystem.cs
│   │   └── VisionSystem.cs
│   ├── Pathfinding/
│   │   ├── Grid.cs
│   │   ├── AStar.cs
│   │   └── FlowField.cs         (later)
│   ├── Data/
│   │   ├── UnitData.cs          Stats loaded from JSON
│   │   ├── BuildingData.cs
│   │   └── Database.cs
│   └── Simulation.csproj
│
├── Presentation/         Godot-aware code
│   ├── Scenes/                  .tscn files
│   ├── Sprites/                 Sprite controllers
│   ├── UI/                      Menus, HUD, panels
│   ├── Audio/
│   ├── Camera/
│   ├── Input/                   Mouse, keyboard, hotkeys
│   └── Renderer/                Reads sim state, draws
│
├── Networking/
│   ├── LockstepClient.cs
│   ├── LockstepServer.cs
│   ├── CommandSerialization.cs
│   └── Networking.csproj
│
├── AI/
│   ├── AIController.cs           A "player" that issues commands
│   ├── Strategy/                 High-level: what to build, when
│   ├── Tactics/                  Mid-level: where to fight
│   └── Micro/                    Low-level: individual unit decisions
│
└── App/
    ├── Main.cs                   Entry point
    └── App.csproj
```

## Tick model

The simulation runs at 20 ticks per second. One tick is 50ms of simulated time.

```csharp
// Conceptual main loop
while (running) {
    var realTimeNow = GetRealTime();
    var ticksToProcess = (realTimeNow - lastTickTime) / TICK_DURATION;

    for (int i = 0; i < ticksToProcess; i++) {
        var commands = inputQueue.DrainForTick(currentTick);
        world.Step(currentTick, commands);
        currentTick++;
        lastTickTime += TICK_DURATION;
    }

    renderer.Draw(world, interpolationAlpha);
}
```

Lockstep networking adds a 2 to 4 tick command delay so all clients have everyone's input before processing the tick.

## Pathfinding

Start simple, evolve as needed.

**Phase 1:** A* on a uniform grid. Units have a per-unit path, computed when commanded. Pathing tile size is 1x1 game tile.

**Phase 2:** When unit counts climb (50+ moving units), introduce flow fields for group movement. Compute one flow field per group destination. Individual A* still used for single-unit moves.

**Phase 3:** Hierarchical pathfinding only if Phase 1 and 2 prove insufficient.

Don't write Phase 3 before Phase 1 works. Don't write Phase 2 before benchmarks show A* is the bottleneck.

### Phase 1 details (as of M1)

Per `src/Simulation/Pathfinding/AStar.cs`:
- 8-connected grid, integer costs (10 orthogonal, 14 diagonal), octile heuristic.
- No corner-cutting: a diagonal step requires both flanking cardinal tiles to be passable.
- Tie-breaker on the open set: lower f-score, then lower h-score, then lower node ID
  (`y * Grid.Width + x`). The heap never breaks ties by insertion order or memory address —
  both vary across runs and would silently desync replays. **Do not change this contract**
  without also updating the determinism test fixtures.
- Path is start-inclusive and goal-inclusive (`Path[0] == startTile`, `Path[^1] == goalTile`).
- Optional string-pulling smoother lives behind `AStar.EnableSmoothing` and is off by default.
  Enabling it changes resulting hashes; must stay off until validated against the
  determinism suite.

### Re-pathing behavior (as of M1)

A unit's path can become invalid mid-walk — most commonly when another unit blocks the next
waypoint (transient blocker) but also when terrain changes underneath it (a building gets
constructed across the path). The MovementSystem handles both with the same machinery:

1. **Wait.** When the next-step tile is occupied by another unit, the unit transitions to
   `UnitState.Waiting` and increments `WaitTicks`. It does not move.
2. **Re-path.** After 5 consecutive wait ticks, the unit emits a fresh path request from its
   current tile to its existing destination. The block may have cleared by the time A* runs;
   if not, A* will route around it.
3. **Give up.** Re-paths are budgeted: at most 3 inside a rolling 100-tick window. The fourth
   re-path within that window aborts the move entirely — the unit goes Idle with no
   destination. This bound is what prevents pathological infinite re-pathing in dense
   settings (a four-unit corridor jam, two villagers shuffling forever, etc.).

A new `MoveCommand` from the input layer resets all of these counters: the user re-issuing a
move is not the same as the engine auto-retrying.

## Entity model

Entities use composition, not inheritance. A `Unit` has components:

```csharp
public sealed class Unit {
    public EntityId Id { get; }
    public PlayerId Owner { get; set; }
    public UnitTypeId Type { get; }
    public FixedVector2 Position { get; set; }
    public Fixed64 Facing { get; set; }
    public Fixed64 HpCurrent { get; set; }
    public IUnitBehavior Behavior { get; set; }  // state machine
    public CarriedResource Carrying { get; set; }
    // etc
}
```

The `Behavior` field holds the current state of a hierarchical state machine (Idle, Moving, Attacking, Gathering, etc). State transitions happen on tick boundaries.

This is similar in spirit to openage's "activity graph" but simpler. We can upgrade to a full graph system later if we need modder-configurable behavior.

## Networking

Lockstep, not state replication. We send commands, not state.

- Each client runs the same deterministic simulation
- Commands are tagged with the tick they should execute on (current + N, where N is the lockstep delay)
- The server (or peer-to-peer leader) collects commands for tick T from all players and broadcasts them
- All clients process tick T only when they have all players' commands for T
- Out-of-sync detection: each client computes a hash of world state every K ticks and compares

For v1, peer-to-peer with one client acting as authority. Dedicated server later.

## Asset pipeline

Sprites live in `assets/sprites/<unit_or_building>/`. Each one has:
- `<name>.png` (the spritesheet)
- `<name>.json` (frame data: frame size, animation definitions, anchor point)

A C# importer in `scripts/` reads these and produces Godot `SpriteFrames` resources at build time. Source PNG is the source of truth, not the Godot resource.

Unit and building stats live in `assets/data/units.json` and `assets/data/buildings.json`. These are loaded into the `Database` at startup. No stats are hardcoded in C#.

## What we do NOT do

- We do not use Godot's physics. Movement and collision are sim-side.
- We do not use Godot's Tween for unit motion. Renderer interpolates manually.
- We do not use Godot's RandomNumberGenerator in sim. Use `DeterministicRng`.
- We do not use async/await in simulation code. Tick processing is synchronous.
- We do not cache Node references on sim objects. Sim objects know nothing about nodes.

## Performance budget (target)

- 20 ticks per second sustained
- 200 units per side, 400 total, without dropping ticks
- A single tick must process in under 25ms on a midrange laptop (half the tick budget, leaving room for rendering)
- 60 FPS rendering at 1080p

We measure regularly. Performance is a feature.
