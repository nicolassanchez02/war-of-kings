# War of Kings

A small, finished real-time strategy game in the spirit of Age of Empires II. One civilization, two ages, deterministic multiplayer, original assets.

This is a focused project. The goal is to ship a polished 1v1 skirmish RTS, not to clone every feature of AoE. Scope discipline is the single most important rule in this repo.

## Status

M0 + M1 + M2 mechanically complete. Branch `overnight/2026-05-11-mechanical-complete` carries the M1/M2 work; tags `m1-complete` and `m2-complete` mark the milestone closes. M3 onward is the next session's work. See [PR #1](https://github.com/nicolassanchez02/war-of-kings/pull/1) and `docs/sessions/2026-05-11-02-overnight-mechanical-complete.md` for the wake-up package.

## How to play (M2 preview)

```powershell
pwsh scripts/smoke-test.ps1     # must be green
pwsh scripts/play.ps1            # launches the M2 scene
```

Controls in the M2 preview:

- **WASD** or cursor near a screen edge: pan camera
- **Mouse wheel**: zoom (5 discrete levels)
- **Left-click on a P1 unit**: select it (drag for box-select; Shift adds to selection)
- **Right-click on terrain**: move the selection there
- **F3**: toggle debug panel
- **F8**: toggle render mode (primitives ↔ sprites; sprites unavailable until Part 6 / asset pipeline lands)

The HUD top bar shows tick count, state hash, FPS, and current zoom. The state hash updates every tick — if you want to verify determinism by eye, run two instances and watch the hash sequences agree.

## What's next

- **First morning task:** smoke-test → launch the game → walk through M2 → log feedback in `docs/OPEN_QUESTIONS.md` (esp. Q-9).
- **Roadmap:** `docs/SCOPE.md` M3 onward. The overnight wake-up note recommends scoping the next session to one milestone at a time.
- **Open decisions for review:** `docs/OPEN_QUESTIONS.md` — 12 entries from the overnight push; top 3 flagged at the top of the doc.
- **Balance:** `docs/BALANCE.md` will land alongside M5 (full roster); does not exist yet.

## Stack

- Godot 4.6.2 (Mono / .NET build — exact version, the `Godot.NET.Sdk` reference in `WarOfKings.Game.csproj` must match)
- C# 12 / .NET 8
- Fixed-point math for deterministic simulation (see `src/Simulation/Core/Fixed64.cs` and `FixedMath.cs`)
- Lockstep networking over ENet (Godot's built-in) — arrives in M8
- Asset pipeline: PNG sprites + JSON metadata, processed at import

The repo has **two solutions**: `WarOfKings.sln` is CI-pure (simulation + tests + headless app, no Godot reference), and `WarOfKings.Game.sln` is dev-full (adds the Godot renderer). Open the dev-full one in your IDE; let CI keep using the pure one.

## Quick start

```powershell
# Build everything and run all tests (sim only — Godot is a separate project)
dotnet test WarOfKings.sln

# Run the deterministic simulation headlessly (proves the sim works)
dotnet run --project src/App/Headless

# Replay with the same seed twice and confirm hashes match
dotnet run --project src/App/Headless -- --twice --ticks 1000 --every 100

# Play the game (Godot 4.6.2 Mono required, see below)
pwsh scripts/play.ps1

# Open the project in the Godot editor
pwsh scripts/edit-godot.ps1
```

Cross-platform wrappers exist for the headless runner: `scripts/run-headless.{sh,ps1}`.

### Godot setup (one-time)

The renderer requires the **Mono / .NET** build of Godot 4.6.2 — the plain build will not load the C# scripts. Install one of:

```powershell
winget install --id GodotEngine.GodotEngine.Mono
```

…or grab `Godot_v4.6.2-stable_mono_win64.exe` from https://godotengine.org/download/windows/ and update the path inside `scripts/play.ps1` and `scripts/edit-godot.ps1`.

For development workflow with Claude Code, see `docs/AGENT_WORKFLOW.md`.

## Repository layout

```
war-of-kings/
├── docs/             Design and architecture documents
├── src/              Game source (C# scripts + Godot scenes)
├── assets/           Sprites, audio, fonts, data tables
├── scripts/          Build, asset pipeline, dev tooling
├── tests/            Determinism tests, unit tests, replay fixtures
└── README.md
```

## Read first

Before writing or asking Claude Code to write any code, read in order:

1. `docs/DESIGN.md` - What the game is and is not
2. `docs/ARCHITECTURE.md` - How the code is structured and why
3. `docs/SCOPE.md` - The MVP feature list and what is explicitly cut
4. `docs/AGENT_WORKFLOW.md` - How to work with Claude Code on this repo

## License

TBD. Code will be MIT or Apache 2.0. Assets will be original or appropriately licensed (no AoE assets, ever).
