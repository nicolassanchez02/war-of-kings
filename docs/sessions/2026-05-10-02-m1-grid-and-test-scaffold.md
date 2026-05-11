# Session 2026-05-10-02: M1 slice 1 — Grid + skipped determinism test

**Goal:** Per Nick's review of M0, start M1 by writing the random-input determinism test first (tests-first discipline for determinism-critical work) and adding the Grid scaffolding it depends on. Movement system and A* are explicit non-goals of this slice.

## What landed

### `src/Simulation/Pathfinding/Grid.cs`
A 200×200 byte array of `Terrain` (Plain / Forest / Mountain / Water / Gold), with:

- `Generate(seed)` deterministically produces a map from a dedicated RNG seeded as `seed ^ 0xA11BA11BA11BA11B`. The XOR-with-constant ensures grid generation cannot be perturbed by other subsystems' RNG draws (DETERMINISM.md, "subsystem independent seeding").
- `GetTerrain(x, y)` / `SetTerrain(x, y, t)` with bounds checks.
- `IsPassable(x, y)` returns false for Mountain and Water; Plain, Forest, and Gold are walkable. (Forest will be harvestable in M3 and the tile transitions to Plain when the tree is chopped — that's where this design pays off.)
- `HashInto(Fnv1a64)` mixes every tile byte in row-major order. 40,000 byte mixes per call. Measured: a 1000-tick `--twice` headless run takes 3.8s including dotnet startup; the hash itself is well under the per-tick budget. If profiling shows it becoming a hot path later, switch to incremental hashing: track a running grid hash, update on `SetTerrain`.

### `src/Simulation/World.cs`
- New `Map { get; }` property, initialized at construction via `Grid.Generate(seed)`.
- `ComputeStateHash` now folds `Map.HashInto(h)` into the FNV-1a stream after `Rng.HashInto` and before the entity loop. A changed tile changes the world hash — exercised in `GridTests.HashInto_GridIncluded_InWorldHash`.

### `tests/Simulation.Tests/GridTests.cs` (6 new tests)
- Generation is seed-stable across instances.
- Different seeds produce substantially different maps (>25% tile divergence).
- `IsPassable` returns the right answer for every terrain type.
- `HashInto` is stable across grid instances with the same seed.
- A grid change changes `World.ComputeStateHash` — the multiplayer OOS detection blindspot guard.
- Two worlds with the same seed produce identical maps tile-for-tile.

### `tests/Determinism/DeterminismTests.cs` — `RandomMoveCommands_ReplayedTwice_ProduceIdenticalHashes`
Written **for the finished M1 system**, marked `[Fact(Skip = "...")]` until movement lands. CI stays green; the goal is in source.

The test:
1. Spawns 4 units in a diagonal pattern.
2. Generates 1000 ticks of randomly-targeted `MoveCommand`s from a dedicated `inputRng` (seed `worldSeed ^ 0xC0FFEEC0FFEE`). One move per unit every 25 ticks. Targets are guaranteed-passable tiles.
3. Runs the world twice with the same seed, asserts the hash sequences are identical (the determinism contract).
4. Re-runs and asserts unit positions actually changed between t=0 and t=1000 (the **non-trivial** guard — if commands aren't being processed at all, the determinism assertion alone would pass vacuously).

**Goal commit:** the final commit of M1 will be the one that removes the `Skip` argument and the test goes green. That commit is the milestone gate.

## Decisions worth pinning

### Skip-vs-fail for in-progress milestone tests
I write the M1 determinism test now (per the tests-first discipline) but mark it `[Skip = "...reason..."]` instead of letting it fail. Reason: CI red on every push for the duration of M1 work would train us to ignore CI; that's the opposite of the protective gradient we want. The Skip attribute documents the in-progress state explicitly, and the discipline is "remove the Skip in the same commit that completes the system."

### Subsystem-independent RNG seeding
`Grid.Generate` seeds its own RNG as `worldSeed ^ 0xA11BA11BA11BA11B`. The map-generation RNG cannot be perturbed by other subsystems' draws. Same pattern will apply when AI, particle previews, etc. need randomness in M7+. This matches DETERMINISM.md's guidance verbatim ("give each subsystem its own RNG seeded from a derived hash of the master seed").

### Grid hashing strategy: full sweep, not incremental
`Grid.HashInto` walks all 40,000 bytes every call. Alternative is incremental: maintain a running hash, update on `SetTerrain`. I chose full sweep because (a) M1's grid is immutable; (b) M3's mutations are sparse (one tile per chopped tree); (c) the full sweep is simple and demonstrably correct. Switch to incremental only if a profile shows the grid hash becoming a hot path.

## A* tie-breaker (not implemented, pinned now to set the contract)

When the A* implementation lands in M1 slice 2, the tie-breaker for equal f-scores **must** be:

1. Lower h-score first (prefer nodes closer to goal — the classic optimization, also deterministic).
2. Then lower node ID (x * Grid.Width + y) — never by heap-address or insertion order.

The reason this matters: with floats (or even with our integer fixed-point math), ties on f-score happen routinely on uniform-cost grids. Tie-breaking by heap-address or hash code is the classic source of "passes on my machine, desyncs on yours" bugs. Locking the tie-break order here as documented contract means any future change to the A* implementation must preserve it.

## Verification

- `dotnet build WarOfKings.sln --configuration Release` — green.
- `dotnet test WarOfKings.sln --configuration Release` — **56/57 pass, 1 skipped** (the M1 target). Sim tests went 49 → 55 (six new Grid tests); determinism tests went 1 → 1-passing-plus-1-skipped.
- `dotnet build WarOfKings.Game.sln --configuration Release` — green.
- `bash scripts/check-sim-purity.sh` — clean.
- `Godot --headless --quit-after 30` — exit 0, no exceptions.
- Headless `--twice --ticks 1000` end-to-end: 3.8s, two runs produce identical hashes.

## What's next (M1 slice 2)

Implement `MoveCommand` processing in `World.Step` plus a minimal movement system: units advance toward `Target` at `MoveSpeed` per tick, stop on arrival. **No pathing** in this slice — straight-line movement. Obstacles, A*, and tile occupancy follow in slices 3 and 4. After slice 2 lands, the skipped determinism test should pass on flat terrain; it will start failing again in slice 3 when obstacles appear, and pass again when A* lands.

## Open questions worth Nick's batched answer (none blocking)

None new this session.

## Trench notes

Mirrored from `docs/AGENT_WORKFLOW.md`. Source of truth is the workflow doc.

- **2026-05-10: WinGet's `Links\godot.exe` shim is broken.** Use the full Mono exe directly via the play/edit scripts.
- **2026-05-10: Two solutions, two purposes.** `WarOfKings.sln` is CI-pure (no Godot). `WarOfKings.Game.sln` is dev-full (adds the Godot project).
- **2026-05-10: .NET 10 SDK defaults to `.slnx`.** New solutions must be scaffolded with `dotnet new sln --format sln`. Also: when adding a Godot.NET.Sdk project to a solution, add `<Configurations>Debug;Release</Configurations>` to the csproj or `dotnet sln add` will map Release→Debug.
