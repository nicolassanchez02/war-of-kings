# Open Questions

This document is the morning review queue. Every judgment call Claude Code made during the overnight push lives here, tagged so you can scan it on your phone over coffee and decide which ones to revisit.

Read the entries top-to-bottom on the first pass. They are ordered roughly by impact — items at the top are the ones where changing my pick gives you the biggest game improvement for the smallest effort.

## Top 4 to look at first

1. **Q-17** (Sprite ↔ entity mapping) — you said you'd want to review this. Five mapping decisions; see the table.
2. **Q-18** (buildings/ LICENSE.md has placeholder fields) — blocker for the repo ever going public. Confirm sources before then.
3. **Q-9** (M2 visuals were never eyeball-tested) — first morning task. Launch the game, walk the UI, log what's wrong.
4. **Q-2** (Brief scope vs session reality) — affects how you write tomorrow's brief. Scope smaller, please.

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
**Context:** The brief asks for M1-M9 plus asset pipeline, AI, lockstep networking, replays, and map editor — six months of milestones — in a single overnight push. I'm a Claude Code session, not an autonomous overnight build farm; I make tool calls turn-by-turn within one session window. Even at the brief's own deferral hierarchy minimum (Parts 0, 1, 2, 3, 4, 5, 7-Easy/Normal, 11), that is still many sessions of work.
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
**Context:** I (Claude Code, overnight) wrote the entire M2 presentation pass — camera, terrain, unit primitives, selection, click-to-move, HUD — by editing the source files and verifying `dotnet build WarOfKings.Game.sln` succeeds. I cannot launch Godot interactively from this environment, so none of the pixel choices, input feel, or visual polish has been eyeball-tested.
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

## Q-12: Starting PopCap = 30, no Houses (single-age scope)
**Context:** The brief calls for Houses that raise population cap by 5 each. To get to a playable M3 in one session, I skipped construction (which needs a BuildCommand + villager-build FSM extension + partial-HP construction state). Instead, both players start with PopCap = 30 — effectively "you already have your houses."
**What I picked:** `_world.GetPlayer(P1).PopCap = 30` and same for P2, in `Main.cs` _Ready.
**Alternatives:** Implement Houses with BuildCommand. Material work but the right long-term answer.
**Where to change it:** `src/Presentation/Main.cs` _Ready; also `assets/data/buildings.json` for the House `populationProvided: 5`.
**Reversible?** easy (introduce BuildCommand handler + house entity).
**Nick's call?** maybe — fine for now if you want to feel the eco loop; revisit when training caps out.

## Q-13: Hardcoded unit costs and train times in ProductionSystem
**Context:** Brief says costs live in `assets/data/units.json` and a JSON loader feeds them in. No loader exists yet. M3 hardcodes (villager 50F/500t, militia 60F/20G/420t) inline as `ProductionSystem.CostFood/CostGold/TrainTicksFor`.
**What I picked:** Inline switch-expressions in ProductionSystem.
**Alternatives:** Build a JSON loader now and bind these from `units.json`.
**Where to change it:** `src/Simulation/Systems/ProductionSystem.cs` (`CostFood`, `CostWood`, `CostGold`, `TrainTicksFor`, `PopCostFor`).
**Reversible?** trivial (loader can fill the same numbers from JSON later).
**Nick's call?** maybe — better to ship the loader as part of M5 when unit roster expands; for now editing the C# switch is fine.

## Q-17: Sprite ↔ entity mapping for the asset import slice
**Context:** Imported `sprites/`, `buildings/`, `iso-tiles/` from `archive/war-of-kings-online/`. Picked specific files for each entity type. These are real visual identity decisions you'll want to validate by eye when you next open the editor.

**What I picked:**
| Entity | Sprite path | Notes |
|---|---|---|
| Villager (UnitTypeId 1) | `assets/sources/sprites/farmer_01/farmer_01_1.png` | Gathering-villager character, frame 1. Five-frame walk cycle exists; not yet animated. |
| Militia (UnitTypeId 2) | `assets/sources/sprites/guard/guard_1.png` | "Sovereign soldier" in Emberveil — for us, just a generic medieval guard. Four-frame walk cycle exists. |
| Town Hall (BuildingTypeId 1) | `assets/sources/buildings/castle.png` | Large dark stone castle. 600×600 source — `DrawTextureRect` scales it to the 3×3 footprint. |
| Tree | `assets/sources/iso-tiles/individual/forest-1.png` | One forest tile from the iso pack. 9 forest-* variants and 7 forest-dense-* variants exist — only forest-1 is wired. |
| BerryBush | `assets/sources/buildings/wheat-fields/wheat-fenced-1.png` | Wheat field as a food node. Fits the "food coming from a tended patch" mental model better than a generic bush. 12 wheat-fenced variants exist. |

**Alternatives I considered:**
- Villager: `farmer_02/`, `villager_01/`, `villager_02/`, `blacksmith/`. Farmer_01 read most clearly as "harvester" to me; villager_01/_02 look more generic.
- Militia: `dwarf/` (Ironborn fighter in Emberveil), `shady_guy/` (scout), `captain/` (commander). Guard felt most "default melee infantry" — the other three feel typed.
- TC: `flag-tower-animated.png` is too small for a TC (single tower); `stone-turrets/` would mean composing multiple sprites. castle.png is one image, large, reads as a town center.
- Tree: any of the 16 forest variants. forest-1 is the conventional "first usable" choice. forest-dense variants are visually busier — might be better for thick forest patches in M5 maps.
- BerryBush: I could have skipped the sprite and kept primitives. Wheat fields read clearly as "food, gather here" so I wired it.

**Where to change it:** All paths live as `private const string Sprite*` in `src/Presentation/Main.cs` (top of the class, just after the spriteCache field).

**Reversible?** Trivial — one-line edit per entity.

**Nick's call?** Yes — three things specifically:
1. **Does the militia sprite read right for a 1v1 RTS context?** (guard_1 is a generic medieval guard, no faction colors).
2. **Tree visual: is forest-1 enough, or do you want tree-density variation** (matching tree HP to dense/loose sprite variants)?
3. **BerryBush as a wheat field**: yes/no. If "no, real berries," I'll switch to a primitive or look for a berry-specific tile. The wheat fits more medieval-RTS than a generic bush imo.

## Q-18: buildings/ pack LICENSE.md has placeholder fields
**Context:** `assets/sources/buildings/LICENSE.md` reads "Source: [fill in — where did grass_path_folder come from?]" and "License: [fill in — check your download source]". So we're shipping `castle.png` and the wheat-fields tiles with unconfirmed license metadata. The repo is private so today this is fine; the moment it goes public this becomes a real problem.
**What I picked:** Imported anyway with the LICENSE.md as-is. Flagged the placeholder fields in CREDITS.md and here.
**Alternatives:** (a) Skip the buildings/ pack until you can confirm the source. (b) Find a CC-licensed castle from Kenney or OpenGameArt to replace castle.png.
**Where to change it:** `assets/sources/buildings/LICENSE.md` — populate Source and License fields.
**Reversible?** Trivial — replace with a properly-licensed asset and update CREDITS.md.
**Nick's call?** Yes — before the repo ever becomes public, confirm where castle.png and wheat-fields came from. If you can't trace the source, swap for a CC-licensed alternative.

## Q-19: Top-down renderer drawing isometric tile art
**Context:** The `iso-tiles/` pack is isometric (diamond perspective, 64×32 effective tile shape). Our renderer is top-down (axis-aligned square tiles). The `forest-1.png` tree sprite renders as drawn — without a perspective transform — which means trees look slightly tilted relative to the grid. Same for any other iso tile we might use.
**What I picked:** Use the iso sprites as-is in top-down. Visual mismatch is small enough to be "passable medieval RTS" rather than "obviously wrong."
**Alternatives:** (a) Restrict ourselves to perspective-neutral assets (character sprites work fine; iso tiles don't). (b) Switch the renderer to true isometric — see `docs/POSTLAUNCH.md`.
**Where to change it:** `src/Presentation/Main.cs` — either remove the tree/bush sprite consts, or do the iso renderer rewrite.
**Reversible?** Easy now, costly once the project has lots of iso art baked in.
**Nick's call?** Maybe — wait until you've launched the game and seen how bad/fine the mismatch looks. If "fine," leave it. If "weird," either revert trees to primitives or schedule the iso renderer rework.

## Q-15: TC trains everything (no Barracks yet)
**Context:** Real RTS pattern is "TC trains villagers; Barracks trains military." For M4 mvp I had both 'V' and 'M' hotkeys queue at the TC — so the TC trains both villagers AND militia. This is wrong long-term but simpler than wiring a second training building.
**What I picked:** Both villager and militia train at TC. The 'M' hotkey finds P1's TC just like 'V' does.
**Alternatives:** Add a Barracks building entity, BuildCommand to construct it, and route militia training through it.
**Where to change it:** `src/Presentation/Main.cs` (`IssueTrainUnit`), `src/Simulation/Systems/ProductionSystem.cs` (no change needed — already type-agnostic).
**Reversible?** easy (just need a Barracks entity + UI routing).
**Nick's call?** yes — the moment Barracks lands, restrict TC to villagers.

## Q-16: P2 is static (no AI / no auto-engage)
**Context:** Right now P2 starts with 3 villagers + a TC and never does anything. It's target practice, not an opponent. Adding even minimal auto-engage (militia attacks nearby enemies on sight) would make P2 fight back. I held off because it's a hidden determinism risk (every behavior decision must be deterministic) and would need a fresh test.
**What I picked:** No P2 behavior at all. Game is playable as target practice.
**Alternatives:** (a) Minimal auto-aggression: every N ticks, military units scan within sight radius and Pursue the nearest enemy. (b) Defensive auto-retaliate: when attacked, set Pursuing against the attacker. (c) Full M7 AI (months of work).
**Where to change it:** New system `src/Simulation/Systems/AutoAggressionSystem.cs` plus an entry in `World.Step`'s pipeline.
**Reversible?** easy.
**Nick's call?** yes — (a) or (b) is the right next step; pick which.

## Q-14: 'V' hotkey trains a villager at the first owned TownHall
**Context:** A real "click building, then click train icon" flow needs building selection in the renderer (which doesn't exist yet — only unit selection does). I picked a hotkey-only path for M3: pressing V queues a villager at P1's lowest-EntityId TownHall.
**What I picked:** 'V' key handler in `Main.cs` that finds the first P1 TC and emits a TrainCommand.
**Alternatives:** (a) Click on a TC to select it, then click a "Train Villager" button in a building command panel. The right long-term UX. (b) Multiple hotkeys for different buildings (V/M/A/K).
**Where to change it:** `src/Presentation/Main.cs` (`IssueTrainVillager`, `_UnhandledInput`).
**Reversible?** easy.
**Nick's call?** yes — once you can click TCs to select them, ditch the keyboard-only flow.

*End of overnight session. Total entries: 20 (Q-1 through Q-19 + Q-Godot). Append more on the next push.*
