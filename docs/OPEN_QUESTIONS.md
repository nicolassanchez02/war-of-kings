# Open Questions

This document is the morning review queue. Every judgment call  made during the overnight push lives here, tagged so you can scan it on your phone over coffee and decide which ones to revisit.

Read the entries top-to-bottom on the first pass. They are ordered roughly by impact — items at the top are the ones where changing my pick gives you the biggest game improvement for the smallest effort.

## Format

Each entry:

- **Context:** What was the situation
- **What I picked:** The decision
- **Alternatives:** What else made sense
- **Where to change it:** Filepath/line/config key
- **Reversible?** trivial / easy / hard / costly
- **Nick's call?** yes / no / maybe — my read

---

## Q-1: Brief described state-of-repo that's already fixed
**Context:** The overnight brief's Part 0.1 and Part 0.2 instructed me to fix `WarOfKings.Game.sln` Release mapping at lines 88-93 and tighten `WarOfKings.Game.csproj` to an allowlist. Both were already done in commit `7ef38d9` ("Cleanup before M1") before this session began.
**What I picked:** Skip both items as already-correct; note here.
**Alternatives:** Mechanically run `dotnet sln remove`/`add` per brief (no-op, churn).
**Where to change it:** N/A — verification only.
**Reversible?** trivial
**Nick's call?** no — clearly the right move

## Q-2: The overnight brief's stated scope is months of work, not one session
**Context:** The brief asks for M1-M9 plus asset pipeline, AI, lockstep networking, replays, and map editor — six months of milestones — in a single overnight push. I'm a  session, not an autonomous overnight build farm; I make tool calls turn-by-turn within one session window. Even at the brief's own deferral hierarchy minimum (Parts 0, 1, 2, 3, 4, 5, 7-Easy/Normal, 11), that is still many sessions of work.
**What I picked:** Execute Part 0 in full, then drive Part 1 (M1 close) as far as one session allows, then write a wake-up package documenting honestly what's done vs. not. Everything beyond Part 1 will likely be untouched and will be clearly flagged as such in the wake-up note.
**Alternatives:** (a) Pretend to do everything via shallow stubs in many files — produces a broken trunk and burns your morning. (b) Refuse the whole brief — leaves M1 unfinished and you with no progress. (c) What I picked: honest, focused, deep on one thing.
**Where to change it:** Mindset — there's no engineering choice here, just a reality check.
**Reversible?** trivial
**Nick's call?** maybe — if you genuinely want me to spread thin and try a little of every part, say so and I'll do that next session; I think the focused approach is better.

## Q-3: GitButler workspace hooks were removed when branching off
**Context:** Creating the `overnight/2026-05-11-mechanical-complete` branch from `gitbutler/workspace` triggered GitButler to remove its managed `pre-commit` and `post-checkout` hooks with the note: "To return to GitButler mode, run: but setup".
**What I picked:** Left as-is — the overnight branch is a normal git branch and the smoke-test pre-push hook is what we want.
**Alternatives:** Re-run `but setup` after the overnight push lands.
**Where to change it:** `but setup` from the command line.
**Reversible?** trivial — one command.
**Nick's call?** yes — you know your GitButler workflow; reinstate when ready.

## Q-4: Villager move speed default (M1 placeholder)
**Context:** Units need a per-tick movement speed. Picked 0.045 tiles/tick (≈ 0.9 tiles/sec at 20 Hz) as a villager-ish default. Stored as `Fixed64.FromRaw(2949)`.
**What I picked:** `MoveSpeedPerTick = Fixed64.FromRaw(2949)` for every unit by default.
**Alternatives:** Per-unit-type speeds loaded from `units.json` (the right long-term answer; deferred to M3/M5).
**Where to change it:** `src/Simulation/Entities/Unit.cs` (`MoveSpeedPerTick` field), eventually `assets/data/units.json`.
**Reversible?** trivial (per-unit), easy (JSON-driven).
**Nick's call?** maybe — this is a feel knob; you'll set it once you can actually watch units walk in M2.

## Q-5: Wait/Repath thresholds (5 wait ticks, 3 repaths per 100-tick window)
**Context:** The brief specified exactly these numbers and I used them as-is. They're the bound between "I'm patient, the corridor will clear" and "I'm stuck, give up." Feels right but never tested in a real match.
**What I picked:** `WaitTicksBeforeRepath = 5`, `MaxRepathsInWindow = 3`, `RepathWindowLengthTicks = 100` (all in `MovementSystem.cs`).
**Alternatives:** Tighter (more responsive but more thrashing) or looser (more patient but units feel "stuck"). Best tuned in the test arena (Part 3.7) once that lands.
**Where to change it:** `src/Simulation/Systems/MovementSystem.cs` constants.
**Reversible?** trivial.
**Nick's call?** maybe — eyeball test once you can watch units in M2.

## Q-6: A* path-smoothing flag default OFF
**Context:** The brief asked for an optional string-pulling smoother that drops mid-path waypoints when a straight line is unobstructed. Off by default because enabling it changes state hashes, and we want the determinism suite to lock in the un-smoothed behavior first.
**What I picked:** `AStar.EnableSmoothing = false` default. Smoother implemented but unused.
**Alternatives:** Default-on with a separate set of determinism tests that pin smoothed-path hashes; more setup but smoother visuals.
**Where to change it:** `src/Simulation/Pathfinding/AStar.cs` (the bool property), and any caller that wants smoothing.
**Reversible?** easy (flip a flag, re-record determinism hashes).
**Nick's call?** no for now; revisit in M2 when you can see the zig-zags.

## Q-7: Hashing the full path on every Unit hash
**Context:** `Unit.HashInto` mixes the entire `Path` list contents into the state hash. This is the safest "every gameplay-relevant byte is in the hash" rule, but for 100 moving units with ~100-tile paths, that's 10k extra mixes per tick. Cheap (FNV is fast), but worth flagging.
**What I picked:** Hash the full path. Safety over speed at M1; revisit only if profiling shows it matters.
**Alternatives:** Hash only `Path.Count` + `PathIndex` + `DestinationTileIdx` and rely on A* determinism to make the actual path content redundant. Faster, but fragile: any future tweak to A* that produces a different (still-valid) path would silently desync replays.
**Where to change it:** `src/Simulation/Entities/Unit.cs` (`HashInto`).
**Reversible?** trivial.
**Nick's call?** maybe — recommend keeping as-is until profiling demands a change.

## Q-9: M2 rendering was written without an interactive Godot session
**Context:** I (, overnight) wrote the entire M2 presentation pass — camera, terrain, unit primitives, selection, click-to-move, HUD — by editing the source files and verifying `dotnet build WarOfKings.Game.sln` succeeds. I cannot launch Godot interactively from this environment, so none of the pixel choices, input feel, or visual polish has been eyeball-tested.
**What I picked:** Compile-clean defaults. Camera pan 600 px/sec at zoom 1.0, edge-pan margin 20 px, zoom levels {0.5, 0.75, 1.0, 1.5, 2.0}, 32 px per tile, click-select radius 25 px, terrain colors as documented in `TerrainColor()`.
**Alternatives:** All of the above are tunable knobs. Selection click radius probably wants to be larger (units feel sticky if it's too tight). Terrain colors are programmer-art; will be replaced by Kenney tiles in Part 6.
**Where to change it:** `src/Presentation/Main.cs` — all constants live at the top.
**Reversible?** trivial — change a number, re-run.
**Nick's call?** yes — launch the game, walk through the interactions, log what feels wrong. The visual-feel pass is the morning's first job after smoke test.

## Q-10: F8 sprites toggle is wired but unimplemented (placeholder mode)
**Context:** Part 2.3 of the brief asked for a Primitives ↔ Sprites toggle on F8. I wired the enum and the keybinding, but the actual sprite-loading lives in Part 6 (asset pipeline). Pressing F8 today just prints a console message.
**What I picked:** Toggle behavior is wired; sprite rendering returns to primitives whenever a sprite is missing (which is always until Part 6).
**Alternatives:** Implement the loader stub now even if asset bundles don't exist.
**Where to change it:** `src/Presentation/Main.cs` (`RenderMode` enum, `HandleMouseButton`).
**Reversible?** trivial.
**Nick's call?** no — depends on Part 6.

## Q-11: Single-file Main.cs vs the component split the brief suggested
**Context:** The brief implied separate node classes (`CameraController`, `UnitRenderer`, `BuildingRenderer`, `Hud`, etc.). I bundled everything into one `Main.cs` because (a) it's faster to write a single file than a multi-node scene tree without a way to test the wiring, and (b) the scene tree shape can be re-architected without changing semantics. The current file is ~280 lines and organized into clear regions.
**What I picked:** Single `Main.cs` with regioned sections for input, camera, rendering, HUD.
**Alternatives:** Refactor into separate nodes when more complex behavior arrives in M3/M4. The split makes more sense when each component has its own state (e.g., per-building renderer with its own animation timeline).
**Where to change it:** `src/Presentation/Main.cs` (split candidates: terrain drawing → `TerrainRenderer.cs`, unit drawing → `UnitsRenderer.cs`, HUD → `Hud.cs` as a `CanvasLayer`).
**Reversible?** easy.
**Nick's call?** yes — long-term you want the split; short-term, single-file is faster to iterate.

## Q-Godot: C:\Godot holds the non-Mono Godot binaries; project requires Mono build
**Context:** Mid-session you mentioned "the godot stuff lives here btw C:\Godot". What's actually at `C:\Godot` is the **plain** Godot 4.6.2 (`Godot_v4.6.2-stable_win64.exe` and the console variant). That build cannot load this project's C# scripts. The **Mono** build required by `WarOfKings.Game.csproj` lives at the original WinGet path: `C:\Users\Nick\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_Microsoft.Winget.Source_8wekyb3d8bbwe\Godot_v4.6.2-stable_mono_win64\`.
**What I picked:** No change. `scripts/play.ps1` and `scripts/edit-godot.ps1` correctly point at the Mono build.
**Alternatives:** (a) Move the Mono build to `C:\Godot\Mono\` so all Godot lives under one root, then update the two scripts. (b) Keep them as-is.
**Where to change it:** `scripts/play.ps1` line 5, `scripts/edit-godot.ps1` line 4 (`$godot = ...`).
**Reversible?** trivial.
**Nick's call?** yes — if you want a single tidy Godot folder, say so and I'll relocate + update the scripts. If you just had the non-Mono build for general Godot work, leaving as-is is correct.

## Q-8: Multi-unit move spread uses a fixed 49-slot spiral
**Context:** A `MoveCommand` carrying N units distributes them around the target tile via a precomputed spiral of 49 offsets (covers up to 7x7). If N > 49 or the 49 slots are all impassable, units fall back to the original target tile and rely on MovementSystem collisions to sort it out.
**What I picked:** 49-slot spiral, hard-coded array in `CommandProcessor.cs`.
**Alternatives:** Generate spiral on demand (handles any group size), or use a flow-field-style cluster solver (overkill until M3).
**Where to change it:** `src/Simulation/Commands/CommandProcessor.cs` (`SpiralOffsets`).
**Reversible?** easy.
**Nick's call?** no — M1 group sizes are small; revisit only if v1 has 50+ unit selections.

---

(More entries will be appended as work proceeds.)
