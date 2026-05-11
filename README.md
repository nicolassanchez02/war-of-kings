# War of Kings

A small, finished real-time strategy game in the spirit of Age of Empires II. One civilization, two ages, deterministic multiplayer, original assets.

This is a focused project. The goal is to ship a polished 1v1 skirmish RTS, not to clone every feature of AoE. Scope discipline is the single most important rule in this repo.

## Status

M0 complete. M1-M9 in flight on `overnight/2026-05-11-mechanical-complete` (see PR for details). Awaiting morning review.

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
