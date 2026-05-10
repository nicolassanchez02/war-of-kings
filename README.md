# War of Kings

A small, finished real-time strategy game in the spirit of Age of Empires II. One civilization, two ages, deterministic multiplayer, original assets.

This is a focused project. The goal is to ship a polished 1v1 skirmish RTS, not to clone every feature of AoE. Scope discipline is the single most important rule in this repo.

## Status

Pre-alpha. Setting up architecture and tooling. No gameplay yet.

## Stack

- Godot 4.3+ with C# (Mono)
- C# 12 / .NET 8
- Fixed-point math for deterministic simulation
- Lockstep networking over ENet (Godot's built-in)
- Asset pipeline: PNG sprites + JSON metadata, processed at import

## Quick start

```bash
# Build everything and run all tests
dotnet test WarOfKings.sln

# Run the deterministic simulation headlessly (proves the sim works)
dotnet run --project src/App/Headless

# Convenience wrappers
bash   scripts/run-headless.sh                  # Linux/macOS/git-bash
pwsh   scripts/run-headless.ps1                 # Windows PowerShell

# Replay with the same seed twice and confirm hashes match
dotnet run --project src/App/Headless -- --twice --ticks 1000 --every 100
```

The Godot editor is not required to build, test, or run the headless simulation.
It will be wired in during M2 (rendering) per `docs/SCOPE.md`.

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
