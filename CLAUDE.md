# Claude Code Quickstart

If you're Claude Code reading this for the first time, here's what you need to know in 60 seconds.

## What this is

**War of Kings.** A small finished real-time strategy game in the spirit of Age of Empires II. Solo developer + you. Six month target. Godot 4 + C#.

## Read these next

In order:
1. `docs/DESIGN.md` - The game
2. `docs/ARCHITECTURE.md` - The code structure
3. `docs/SCOPE.md` - The milestones and the things explicitly cut
4. `docs/DETERMINISM.md` - The rules that keep multiplayer working
5. `docs/AGENT_WORKFLOW.md` - How we work together

## Non-negotiable rules

1. **Simulation code (anything under `src/Simulation/`) has no `using Godot;`.** Ever.
2. **No `float`, `double`, `System.Random`, `Math.X`, `DateTime`, `Stopwatch` in simulation code.** Use `Fixed64`, `FixedMath`, `DeterministicRng`, `currentTick`.
3. **Iterate entities in `EntityId` order, always.** No `Dictionary` foreach in simulation logic.
4. **Stats live in JSON, not C#.** `assets/data/units.json` and `assets/data/buildings.json`.
5. **Don't add features not in `docs/SCOPE.md` for the current milestone.** If the human asks, push back and ask which milestone item it belongs to.

## Where you are right now

This repo is at **M0: Foundation**. The work for M0 is in `docs/SCOPE.md` under "M0: Foundation". The stubs that need implementing are in `src/Simulation/Core/`. Specifically:

- `FixedMath.Sqrt`, `Sin`, `Cos`, `Atan2` are stubs marked with `TODO(Claude Code, M0)`
- `World.ComputeStateHash` returns 0; needs a real hash implementation
- `World.Step` doesn't process commands yet
- `DeterministicRngTests.KnownSequence_IsStable` needs the canonical reference values

When picking up work, do one M0 checklist item at a time. Confirm tests pass. Move on.

## How to start a session

1. Tell me which milestone item from `docs/SCOPE.md` you want to work on.
2. Or ask me to suggest the next-most-valuable thing to do.
3. I'll read the relevant docs, write the code, write the tests, run them.

I will refuse (politely) to:
- Add features outside the current milestone.
- Put floats or non-deterministic constructs in simulation code.
- Skip the determinism test.
- Start a major refactor without us discussing the trade-offs first.

That's the deal. Let's build something good.
