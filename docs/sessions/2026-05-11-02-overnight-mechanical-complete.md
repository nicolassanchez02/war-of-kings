# Overnight Session: Mechanical Completion (partial)

**Date:** 2026-05-11 evening → 2026-05-12 morning
**Branch:** `overnight/2026-05-11-mechanical-complete`
**PR:** [#1 (draft)](https://github.com/nicolassanchez02/war-of-kings/pull/1)
**Author:** Claude Code (Opus 4.7)
**Tags landed:** `m1-complete`, `m2-complete`

## Headline

Three milestones moved forward in one Claude Code session: **Part 0 (session-zero)**, **Part 1 (M1 close — pathfinding, movement, determinism)**, and **Part 2 (M2 rendering — camera, terrain, units, selection, click-to-move, HUD)**. Parts 3 through 10 (economy, combat, full roster, fog, AI, networking, replays, map editor, asset pipeline) are **not started** — that scope is genuinely months of work and could not be even half-honest in the time available.

## The honest framing (read this first)

The overnight brief described M1 through M9 plus asset pipeline plus AI plus lockstep networking plus a map editor as a single overnight push. Claude Code runs turn-by-turn within one session window — not autonomously while you sleep. So this session went deep on the parts I could actually do well, and stopped at the point where continuing would have meant committing half-finished half-working systems that would have left the trunk worse, not better.

What you got:
- **Real M1.** Production-quality A*, movement, occupancy, command processing. Five determinism tests pass. 75 sim tests pass. The branch is mergeable as-is.
- **Real M2 (mechanically).** Camera, terrain, primitives unit rendering, drag-box selection, click-to-move, HUD. **Not visually verified** — see Q-9. You'll want to launch it first thing and walk through the interactions.
- **No M3+.** Untouched. Roadmap below for how to pick up.

## What's now true

- `overnight/2026-05-11-mechanical-complete` branch has 3 commits beyond `main`
- Draft PR #1 is open on GitHub, with a structured status board in its description
- Tags: `m1-complete` (after M1), `m2-complete` (after M2)
- `scripts/smoke-test.{ps1,sh}` exists and is green at every commit on the branch
- `scripts/hooks/pre-push` + `scripts/install-hooks.ps1` for opt-in pre-push smoke
- `docs/OPEN_QUESTIONS.md` has 11 entries with full context for every judgment call
- `docs/ARCHITECTURE.md` updated with the Phase-1 pathfinding contract (tie-breaker, costs, no corner-cutting, re-pathing rules)
- `docs/SCOPE.md` M1 checked off in full, M2 checked off with two `[~]` partial markers
- 75 sim unit tests pass; 5 determinism tests pass (was 1+1; M1 random-input de-skipped, plus 100-unit pathing, narrow corridor, mid-walk re-path)
- The headless `--twice` determinism run is green
- Memory updated re: `C:\Godot` vs the Mono build location

## Part-by-part status

| Part | Title | Status |
|------|-------|--------|
| 0    | Clear the deck | ✅ done |
| 1    | M1 close (Move + A* + Movement + tests) | ✅ done |
| 2    | M2 Rendering | ✅ done (compile-clean; not visually verified — see Q-9) |
| 3    | M3 Economy + M4 Combat | ⛔ not started |
| 4    | M5 Full roster + BALANCE | ⛔ not started |
| 5    | M6 Fog of war | ⛔ not started |
| 6    | Asset pipeline (Kenney) | ⛔ not started |
| 7    | M7 AI opponent | ⛔ not started |
| 8    | M8 Networking | ⛔ not started |
| 9    | M9 Replays | ⛔ not started |
| 10   | Map editor + Crossroads | ⛔ not started |
| 11   | Wake-up package | ✅ done (this note) |

## What you should do this morning, in order

1. `git fetch && git checkout overnight/2026-05-11-mechanical-complete`
2. `pwsh scripts/smoke-test.ps1` — green is required before doing anything else
3. `pwsh scripts/play.ps1` — **first time you actually see units walking around**. Verify:
   - WASD pans the camera; edge-pan works; mouse wheel zooms
   - Left-click selects a P1 unit; drag-box selects multiple
   - Right-click on empty terrain issues a move; the unit pathfinds toward it
   - HUD top bar shows tick, hash, FPS, zoom
   - F3 toggles a small debug panel
4. Read `docs/OPEN_QUESTIONS.md` end-to-end. There are 11 entries. The top 5 are the most impactful. Decide which to act on.
5. Read this note's "What's NOT done" section to set your expectations for M3+ work.
6. Decide on M3 strategy: tomorrow's brief should be much smaller and more scoped — one Part at a time, not nine.

## What's NOT done (deliberately)

- **M3 economy**: resources, gathering, drop-off buildings, training queues, construction, build menu. Villager FSM. This was a huge swing and would have produced broken state with the time available.
- **M4 combat**: damage, armor, stances, attack-move, building destruction, win condition. Depends on M3 working.
- **M5 roster expansion**: Castle Age, all 8 units, all 11 buildings, counter tuning, BALANCE.md.
- **M6 fog of war**: per-player vision grid, fog overlay, vision-aware combat.
- **Asset pipeline (Part 6)**: Kenney download, importer, sprite swap, SFX. F8 toggle is wired but always falls back to primitives because no assets exist yet.
- **M7 AI**: strategic/tactical/micro layers, three difficulties, decision logging, AI vs AI test harness. The largest single missing piece.
- **M8 networking**: ENet lockstep, OOS detection, desync bundles, lobby.
- **M9 replays**: recording, playback, replay-as-regression-test runner.
- **Part 10 map editor and Crossroads map**: a hand-tuned starter map and the in-game editor for tuning it.

If you re-issue the overnight brief tomorrow night, I strongly suggest scoping it to one milestone (e.g., "M3 economy, full implementation") rather than nine.

## Top decisions you might want to revisit

(See `docs/OPEN_QUESTIONS.md` for the full list with rationale and revert paths.)

1. **Q-9: M2 written without an interactive Godot session.** Pixel/feel choices are best-guess. First-morning task is to walk through the UI and log what's wrong.
2. **Q-2: Brief scope was months, not a night.** If you re-issue tomorrow, scope smaller.
3. **Q-4: Villager move speed (`Fixed64.FromRaw(2949)` = ~0.045 tiles/tick).** Placeholder until per-unit-type JSON lands in M3.
4. **Q-5: Wait/repath thresholds (5/3/100).** Brief specified these exactly; gut-test in arena later.
5. **Q-7: Hashing full path on every Unit hash.** Safe but verbose; profile before optimizing.
6. **Q-Godot: `C:\Godot` holds non-Mono binaries.** Scripts correctly point at the Mono build at the WinGet path; only revisit if you want one tidy Godot folder.

## Determinism status

- **Same-platform determinism: verified across 5 scenarios.**
  - Empty world (100 ticks)
  - Random move commands (1000 ticks, 4 units) — the canonical M1 contract
  - 100-unit random pathing (500 ticks)
  - Narrow-corridor contention (600 ticks)
  - Mid-walk path invalidation (800 ticks)
- **Cross-platform determinism: not yet tested.** CI's matrix runs the same suite on Linux/macOS/Windows but does not yet diff hashes across them. The TODO in `.github/workflows/ci.yml` (line 60) covers this; it's a one-evening task for a future session.
- **Headless `--twice` runs match** for the brief's smoke-test scenario.

## Performance snapshot

- Smoke-test sim build: ~2s
- Smoke-test game build: ~4s
- Sim test suite: ~120ms for 70 tests
- Determinism suite: ~2s for 5 scenarios
- Headless 1000-tick `--twice`: well under a second

No FPS/tick-time measurement under load yet — that needs a profiler attached to the running game, which is a morning task.

## Bugs I noticed but didn't fix

- **None known.** All tests green at every commit.
- **Potential issue**: `Main.cs` re-computes the state hash every frame (60 Hz) for the HUD readout, which scales O(N) over entities and grid cells (40000). For M1 unit counts this is fine. At M3 scale (50+ entities + buildings) it might become a visible draw-call cost. Easy fix later: cache the hash, recompute on tick boundary only.

## Trench notes

- The overnight brief was written against a snapshot of the repo that was hours stale: Part 0.1 (Release mapping fix) and Part 0.2 (csproj allowlist) were already done in commit `7ef38d9`. Verified and skipped, noted in Q-1.
- GitButler removed its managed `pre-commit` and `post-checkout` hooks when I branched off `gitbutler/workspace`. Reinstate with `but setup` after this branch merges. Noted in Q-3.
- The smoke-test's headless step initially failed because `--nologo` was being passed through `dotnet run --` as a program argument. Removed and re-ran; fixed in the same commit.
- One A* test (`No_corner_cutting_through_diagonal_block`) was authored expecting a non-existent path through a fully-enclosed corner; corrected to a passable detour scenario that exercises the same invariant.
- Span ref struct can't be captured in a lambda — the first cut of `MovementSystem.ResolvePendingPath` used `world.OccupancyView` (a `ReadOnlySpan<EntityId>`) inside the `isBlocked` callback. Refactored to capture `world` and call `GetOccupant` per-tile.

## Things to celebrate

- **A* tie-breaker contract is real, in code, locked by 5 determinism tests.** The contract pinned in session note 2026-05-10-02 is now mechanically enforced; future refactors will be caught at CI time.
- **5 determinism tests, not 1.** The minimum bar was the random-input replay; we shipped four extra scenarios that exercise occupancy, contention, and re-pathing. That widens the safety net for every subsequent milestone.
- **Render layer compiles cleanly and re-uses the same `World` API the headless runner uses.** No special hooks for the renderer; the sim is canonical and Godot is a viewer.
- **OPEN_QUESTIONS.md format works.** It's now a real artifact that captures the "what I picked and why" for every judgment call. Future overnight pushes can use the same pattern.

## Recommended shape for tomorrow's session

Drop the multi-Part brief approach. Pick one of:

- **Option A — M3 Economy (most impactful):** Resources (Tree/BerryBush/GoldMine), villager FSM, drop-off buildings, gather command, training queues. Test arena scene from Part 3.7 of the original brief. One milestone, one session. Wake up to a working economy loop.
- **Option B — M2 polish (fastest payoff):** Walk through the UI, log what's wrong in Q-9, send me a tightened brief. Maybe 1-2 hours.
- **Option C — Asset pipeline (visual win):** Kenney download, importer, sprite swap. Requires fetching external assets, which means actually running `npm`/`curl` against `kenney.nl` — flag for me to coordinate with you.

I'd rank them: B (today's first morning hour) → A (this week) → C (next week or after M5).
