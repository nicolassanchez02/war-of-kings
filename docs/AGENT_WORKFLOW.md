# Working with Claude Code on this project

This document is for you (the human director) and for Claude Code itself. Claude Code should read it at the start of every session.

## The director's mental model

You are not writing this game. Claude Code is writing this game. You are the technical director and the designer. Your job is:

1. **Decide what to build next.** Pick the next item from `SCOPE.md`.
2. **Specify clearly.** Write a short task brief. Reference the relevant section of the design or architecture docs.
3. **Review diffs.** Especially in the simulation layer. Determinism bugs are expensive.
4. **Playtest.** No agent can tell you whether the game feels good.
5. **Protect the scope.** Say no to features that aren't in `SCOPE.md`.

If you find yourself writing code, ask first whether Claude Code could have written it. The answer is almost always yes. Reserve your time for the things only you can do.

## Session ritual

Every Claude Code session, at the very start:

1. Have Claude Code read `docs/DESIGN.md`, `docs/ARCHITECTURE.md`, `docs/SCOPE.md`, and this file.
2. State which milestone you're working in and which item from its checklist you're doing.
3. State any constraints from previous sessions that aren't yet in the docs (and ask Claude Code to add them).

This sounds heavy. It takes 30 seconds and prevents hours of cleanup later.

## Task brief template

Use this template when assigning a non-trivial task:

```
Milestone: M3 (Economy)
Checklist item: Villager gathers wood from trees

Goal:
A villager unit, when given a Gather command targeting a tree,
walks to the tree, chops it (animation + tick-based wood accumulation),
walks to the nearest wood drop-off building, deposits the wood,
and returns to the same or nearest tree.

Constraints:
- All logic in src/Simulation/Systems/GatheringSystem.cs and related files
- No Godot dependency in the simulation
- Use existing Fixed64, FixedVector2 types
- Determinism: must produce identical state given identical inputs
- Add a determinism test in tests/Determinism/GatheringDeterminismTest.cs

Acceptance:
- Unit test: a villager + tree + town center, run 200 ticks, assert wood went up by N
- Determinism test passes
- Existing tests still pass

Out of scope:
- Auto-targeting next tree (separate task)
- Other resource types (separate task)
- Visual feedback (presentation layer, separate task)
```

The more specific you are, the less you have to correct later.

## Things to watch in code review

**Red flags in simulation code:**

- `using Godot;` in a Simulation/ file. Never allowed.
- `float`, `double`, `Math.X`, `Random` anywhere in Simulation/. Never allowed.
- `DateTime.Now`, `Stopwatch`, `Environment.TickCount`. Never in simulation.
- `foreach` over a `Dictionary` or `HashSet`. Replace with sorted iteration.
- `new Unit(...)` outside the entity factory. Use the factory.
- New entity fields that don't get serialized in the state hash function.

**Red flags anywhere:**

- A new feature appearing that isn't on the milestone checklist.
- A hardcoded unit stat. Stats live in JSON.
- A "TODO: fix later" without a corresponding GitHub issue.
- An async method in simulation code.
- Tests that exercise the renderer instead of the simulation.

## What to delegate freely

These tasks are fast and safe to hand to Claude Code with light review:

- Boilerplate (data classes, serializers, JSON loaders)
- UI panels and menus
- Asset pipeline scripts
- Test fixtures
- Documentation updates
- Refactors with good test coverage
- New unit types that follow the existing pattern

## What to review carefully

- Anything in `src/Simulation/Core/` (Fixed64, math, RNG, World)
- Anything that changes the tick loop
- Anything in pathfinding
- Anything in networking
- Anything that touches the determinism hash function
- The first implementation of any new system (before the pattern is established)

## What to do yourself

- The first 30 minutes after a new feature lands: actually play with it. Does it feel right?
- Tuning numbers (unit stats, costs, build times). Claude Code can guess, but balance is your call.
- Designing the AI's strategic priorities (Claude Code can implement them once specified).
- Choosing the visual style and approving art direction.
- Saying no.

## Long-context discipline

Agent context windows are large but not infinite. Strategies that help:

- Keep design docs short and the source of truth. When something changes, update the doc, don't just tell Claude in chat.
- Use the milestone structure. Don't ask Claude Code to "implement all of M3" in one session. One checklist item at a time.
- After a session, ask Claude Code to summarize what changed and what's still pending. Paste that into your notes for next time.
- If a session is going sideways, end it. Start fresh with a tighter brief. Don't try to recover a confused context.

## Determinism: the one rule that bites if you forget it

The simulation must produce identical state given identical inputs, on any machine, any OS, any time. If this breaks, multiplayer breaks, replays break, the AI's behavior becomes inconsistent, and bugs become impossible to reproduce.

The determinism test in CI is your friend. Don't bypass it. If it fails, find the cause, don't paper over it.

When in doubt: would this code do something different on a Mac vs a Windows machine? On an AMD vs Intel CPU? Across .NET versions? If yes, it doesn't belong in the simulation.

## When Claude Code is wrong

It will be, sometimes. Common failure modes on this project:

- **Floats sneaking into sim code.** Catch in review or via the analyzer (see scripts/).
- **Premature optimization.** It'll want to add caches and pools before they're needed. Push back.
- **Feature creep.** It'll add "while I was in there..." features. Push back hard.
- **Reverting decisions.** It'll occasionally rewrite something that was correct because it didn't read the doc. Point at the doc, ask it to read, try again.
- **Test theater.** Tests that don't actually test anything. Read the assertions.

These are not Claude Code being bad. They're the standard failure modes of any collaborator who doesn't have full context. The fix is the same: better briefs, better docs, better reviews.

## Post-milestone smoke-test (do this BEFORE green-lighting the next milestone)

When Claude Code reports a milestone closed, don't just take its word. Spend ~20 minutes calibrating before authorizing the next milestone. This is also the first thing to run after every M-checklist commit lands on `main`.

1. **See it.** `pwsh scripts/edit-godot.ps1` (or `scripts/play.ps1`). Confirm the scene loads, the HUD tick counter advances at 20 Hz, and the state hash changes/doesn't change exactly as you'd expect for what's supposed to be happening this milestone. (At M0 the hash should be **stable tick-to-tick** — nothing is moving. At M1+ it should change every tick.)
2. **Replay it.** `dotnet run --project src/App/Headless -- --spawn 4 --twice` (adjust `--ticks` to taste). Confirm both runs print identical hash sequences and the "DETERMINISTIC" line at the bottom. This is the determinism contract working in practice — see it with your own eyes before anything else builds on top.
3. **Feel the tests.** `dotnet test WarOfKings.sln`. Note the duration. If it's already over 10 seconds at M0/M1, flag it now — fast tests are a feature and the suite only grows. The threshold is roughly: "tests must run faster than your coffee cools."
4. **Skim the load-bearing diffs.** Spend five minutes reading the actual changes to `src/Simulation/Core/` (Fixed64, FixedMath, DeterministicRng, World) and anything new in `Systems/` or `Pathfinding/`. Especially: state-hash function, tick-loop ordering, A* tie-breakers, RNG seeding. Implicit choices in determinism-critical code are bug magnets months later.

If any of those four fail, don't authorize the next milestone — fix what failed first.

## Trench notes

These are install/toolchain quirks and architectural observations that future-you (or future Claude Code) will hit again. They don't belong in chat — chat evaporates. They don't belong in source comments — most of them aren't about any one file. They live here so they survive.

When a session uncovers one, add it here. Format: short title, one paragraph, dated. Prune entries that have been superseded.

Also reproduce the *current* list (verbatim) under a `## Trench notes` heading at the bottom of each session note in `docs/sessions/`. That keeps the knowledge tethered to the session that proved it.

### 2026-05-10: WinGet's `Links\godot.exe` shim is broken
WinGet installs Godot 4.6.2 Mono at `C:\Users\Nick\AppData\Local\Microsoft\WinGet\Packages\GodotEngine.GodotEngine.Mono_…\Godot_v4.6.2-stable_mono_win64\`, and creates a `Links\godot.exe` shim. The shim **does not forward `--headless --quit` correctly** — Godot hangs forever and the assemblies-not-found error masks the symptom. Always point scripts at the real exe directly. `scripts/play.ps1` and `scripts/edit-godot.ps1` already do this; if Godot is reinstalled to a different path, update the `$godot` line in both files.

### 2026-05-10: Two solutions, two purposes
`WarOfKings.sln` is the CI-pure solution: simulation + tests + headless app. It must never reference the Godot project so CI doesn't need the Godot.NET.Sdk runtime to build. `WarOfKings.Game.sln` is the dev-full solution: the same projects plus `WarOfKings.Game.csproj`. Open this one in Rider/VS for normal day-to-day work. The CI workflow targets `WarOfKings.sln` explicitly; if you change CI to target `WarOfKings.Game.sln` the build agent will need Godot installed.

### 2026-05-10: .NET 10 SDK defaults to .slnx (XML) — CI on .NET 8 can't read it
`dotnet new sln` on the .NET 10 SDK produces `.slnx`, an XML-format slim solution file. `dotnet sln add` against a `.sln` filename when only `.slnx` exists silently fails ("Could not find solution"). `.slnx` is not understood by .NET 8 tooling, which is what CI uses. Always pass `--format sln` when scaffolding new solutions until CI is upgraded.

### 2026-05-10: `dotnet sln add` maps Release→Debug for Godot.NET.Sdk projects
When you `dotnet sln add` a project that uses a non-default SDK (Godot.NET.Sdk is the case we hit), the .sln tooling can't introspect available configurations and silently falls back to mapping every `Release|*` to `Debug|Any CPU`. The Release build of the sln then quietly builds that project in Debug, with all the perf and diagnostic implications. Fix at the source: add `<Configurations>Debug;Release</Configurations>` to the csproj BEFORE running `dotnet sln add`. If it's already in the sln, hand-edit lines 88-93-style or remove-and-re-add.

## Pacing

Aim for one milestone checklist item per session, sometimes two for small ones. A milestone is a couple of weeks of evening work. The whole game is roughly six months.

If you're burning through items in 20 minutes each, you're going too fast and quality will suffer. If you're spending three sessions on one item, the brief is too vague or the architecture is wrong, stop and fix that.

The point is to ship and to enjoy shipping. If a session feels miserable, end it early and pick up tomorrow.
