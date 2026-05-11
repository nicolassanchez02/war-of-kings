# Scope and Roadmap

This document is the single source of truth for what's in v1 and what's not. When you start a Claude Code session, point it at this file and the current milestone.

## v1 definition of done

A player can:
1. Launch the game from a menu
2. Start a skirmish against an AI on the default map
3. Or host/join a 1v1 multiplayer match on LAN
4. Gather all three resources
5. Build all 11 listed buildings
6. Train all 8 listed units
7. Advance to Castle Age
8. Win by destroying the opponent's Town Center
9. Watch a replay of the match they just played
10. Have it feel good doing all of the above

If those 10 items work, we ship.

## Milestones

Each milestone has a definition of done. Don't move to the next one until the current one is met.

### M0: Foundation (week 1 to 2)

**Goal:** A repo that builds, runs an empty Godot scene, and has the simulation library compiling separately.

Definition of done:
- [x] Godot project opens cleanly (Godot 4.6.2 Mono; `pwsh scripts/play.ps1` runs the scene; `pwsh scripts/edit-godot.ps1` opens the editor)
- [x] `Simulation.csproj` builds standalone (no Godot reference)
- [x] Solution builds end to end
- [x] CI runs on push and goes green
- [x] `Fixed64`, `FixedVector2`, `FixedMath` implemented and unit-tested
- [x] `DeterministicRng` implemented and unit-tested
- [x] A "hello sim" test that creates a `World`, adds one entity, steps 100 ticks, and asserts state (see `HelloSimTests`, plus a placeholder `Unit` entity with hashable fields)

### M1: Headless deterministic world (week 3 to 4)

**Goal:** A world where units can move and the sim is provably deterministic.

Definition of done:
- [x] Grid map, 200x200 tiles, each tile has a terrain type
- [x] Entity factory with monotonic IDs
- [x] One unit type (placeholder villager)
- [x] Move command works: click a tile, unit walks there
- [x] A* pathfinding on the grid, avoiding impassable tiles
- [x] Two units cannot occupy the same tile
- [x] **Determinism test:** record 1000 ticks of random input, replay twice, assert identical state hash. Test runs in CI.

### M2: Rendering (week 5 to 6)

**Goal:** You can see what's happening.

Definition of done:
- [x] Camera with pan (WASD) and zoom (mouse wheel)
- [x] Terrain rendered from the grid
- [~] Unit sprite rendered at unit position, interpolated between ticks (sprite is primitives-only this pass; interpolation deferred)
- [x] Selection: click to select, drag-box to multi-select
- [x] Right-click issues move commands to all selected units
- [x] Selection highlight ring under selected units
- [x] Player color tinting on units (primitives use blue/red; sprite tint shader deferred to Part 6)

### M3: Economy (week 7 to 9)

**Goal:** The economy loop works and feels good. This is the most important milestone.

Definition of done:
- [x] Trees, ~~gold mines~~, berry bushes exist on the map (gold mines deferred per single-age scope)
- [x] Villager gathers wood from trees (walk, chop, carry, deposit, repeat)
- [x] Villager gathers food from berries
- [ ] Villager gathers gold from mines _(out of single-age scope)_
- [x] Town Center serves as drop-off _(Lumber Camp / Mill / Mining Camp not built; TC accepts all)_
- [x] Resources display in a HUD
- [x] Trees deplete and disappear (visual shrink at 75/50/25)
- [x] Villagers auto-target the nearest equivalent resource when one depletes
- [ ] Build menu: select villager, click building icon, click placement, villager walks and builds _(deferred)_
- [ ] Houses raise population cap _(deferred — starting cap raised to 30)_
- [x] Town Center trains villagers (queue, cost, time)

### M4: Combat (week 10 to 11)

**Goal:** Things can fight.

Definition of done:
- [~] Militia trains from ~~Barracks~~ Town Hall (Barracks deferred — Q-15)
- [ ] Two militia from different players will attack each other on sight _(no auto-engage; explicit AttackCommand only)_
- [x] HP, damage, armor, attack speed all working _(hardcoded in UnitStats; data-table loader deferred — Q-13)_
- [ ] Attack-move command _(deferred)_
- [ ] Stances _(deferred — assumes default Aggressive for everything)_
- [~] Units die, ~~leave a corpse for a few seconds~~, disappear (no corpse fade)
- [x] Buildings take damage and can be destroyed
- [x] Destroying the Town Center wins the game (placeholder banner in renderer)

### M5: Full roster (week 12 to 14)

**Goal:** All 8 units and 11 buildings exist and work.

Definition of done:
- [ ] All Settlement Age units and buildings implemented
- [ ] Age advancement researched at Town Center
- [ ] All Castle Age units and buildings implemented
- [ ] Each unit has appropriate counters working (spear beats knight, archer beats spear, knight beats archer)
- [ ] Siege beats buildings, dies to anything else
- [ ] Damage types and armor types working correctly

### M6: Fog of war and vision (week 15)

**Goal:** You can't see what you haven't scouted.

Definition of done:
- [ ] Each unit has a vision radius
- [ ] Each building has a vision radius
- [ ] Tiles you've never seen are black
- [ ] Tiles you've seen but don't currently see are dimmed and frozen
- [ ] Enemy units only render in currently-visible tiles
- [ ] Minimap in the HUD shows known terrain and current units

### M7: AI opponent (week 16 to 18)

**Goal:** A scriptable AI that plays a coherent game.

Definition of done:
- [ ] AI controller that issues commands through the same interface as a human player
- [ ] AI builds villagers, gathers resources, expands economy
- [ ] AI advances to Castle Age
- [ ] AI builds military and attacks
- [ ] AI is beatable but not trivially. A new player should lose their first 1 to 3 games.
- [ ] AI difficulty levels: Easy (passive, weaker eco), Normal, Hard (faster eco, smarter attacks)
- [ ] AI uses no information it shouldn't have (respects fog of war)

### M8: Networking (week 19 to 21)

**Goal:** Two humans on a LAN can play a complete match.

Definition of done:
- [ ] Host a game on LAN, another machine can join
- [ ] Lockstep tick synchronization working
- [ ] Command delay tunable, default 4 ticks (200ms)
- [ ] Out-of-sync detection: hash state every 60 ticks, compare across clients
- [ ] A desync produces a useful error and saves a debug bundle
- [ ] A complete 20-minute match plays without desync
- [ ] Internet play works (NAT punch-through or relay, decide based on testing)

### M9: Replays (week 22)

**Goal:** Every match is replayable.

Definition of done:
- [ ] Every match auto-saves a replay file (the seed + the command log)
- [ ] Replays can be played back at 1x, 2x, 4x
- [ ] Camera is free during replay (you can watch any part of the map)
- [ ] Replays are tiny (kilobytes, not megabytes)

### M10: Polish and ship (week 23 to 26)

**Goal:** Ship it.

Definition of done:
- [ ] Main menu, settings menu, in-game menu (pause, resign, options)
- [ ] Sound effects for all actions
- [ ] Music (one or two tracks)
- [ ] Settings: audio, video, hotkeys
- [ ] Loading screen
- [ ] Win and loss screens
- [ ] Replay browser
- [ ] Tutorial mission (one short scripted scenario teaching the basics)
- [ ] At least 20 hours of internal playtesting with no crashes
- [ ] Build pipeline for Windows, macOS, Linux
- [ ] Itch.io page set up
- [ ] Release

## Post-launch wishlist

Tracked in `docs/POSTLAUNCH.md`. Anything that doesn't belong in v1 but is worth not forgetting goes there.

## What gets added if v1 ships and people care

(Don't work on these. Don't even think about them. They live here only so you can stop thinking about them.)

- A second civilization
- A campaign
- 2v2 and FFA
- Random map generation
- More maps
- Walls and gates
- Naval (probably not)
- A scenario editor
- Steam release with workshop support
- Mod support via the data-driven units/buildings JSON

## Things that look small but aren't

- Walls. They wreck pathfinding. Cut.
- Multiple resource drop-off priorities. Cut, just use nearest.
- Custom hotkey rebinding. v1 has fixed hotkeys, settings menu has rebinding only if there's time.
- Achievements. None at launch.
- Cloud save. None at launch.

## Things that look big but aren't

- Save/load mid-match. Trivial because we have deterministic replays already. The "save" is the command log up to now plus the world hash. Resume by replaying it. Implement during M9.
- Pause in multiplayer. Just stop processing tick advances on all clients. Easy.
- Spectator. Trivially supported by replays. Just don't accept commands from the spectator.
