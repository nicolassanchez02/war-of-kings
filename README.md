# War of Kings

A small, finished real-time strategy game in the spirit of Age of Empires II. One civilization, two ages, deterministic multiplayer, original assets.

This is a focused project. The goal is to ship a polished 1v1 skirmish RTS, not to clone every feature of AoE. Scope discipline is the single most important rule in this repo.

## Status

M0 + M1 + M2 + M3 (single-age scope) + M4 MVP mechanically complete. Branch `overnight/2026-05-11-mechanical-complete`; tags `m1-complete`, `m2-complete`, `playable-mvp`. The game is playable end-to-end: gather, train, fight, win. M5+ explicitly cut per single-age scope. See [PR #1](https://github.com/nicolassanchez02/war-of-kings/pull/1) and `docs/sessions/2026-05-11-02-overnight-mechanical-complete.md` for the wake-up package.

## How to play

Open Godot Mono → import `project.godot` → press F5. (Or `pwsh scripts/play.ps1` from the command line.)

Controls:

- **WASD** or cursor near a screen edge: pan camera
- **Mouse wheel**: zoom (5 discrete levels)
- **Left-click** a P1 unit: select. Drag = box-select. Shift = add.
- **Right-click**: context-sensitive
  - on open terrain → move
  - on a Tree or BerryBush → gather (villagers walk over, chop/pick, deposit at TC, repeat)
  - on an enemy unit or building → attack
- **V**: queue a villager at P1's first Town Hall (cost 50 food, 25s)
- **M**: queue a militia at the same TC (cost 60 food + 20 gold, 21s — Q-15: should be a Barracks)
- **F3**: toggle debug panel
- **F8**: toggle render mode (sprites unavailable until Part 6)

Win condition: destroy the enemy Town Center. VICTORY / DEFEAT banner appears when either side's last TC falls. P2 is a static target for now (no AI — see `docs/OPEN_QUESTIONS.md` Q-16).

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

Before writing any code, read in order:

1. `docs/DESIGN.md` - What the game is and is not
2. `docs/ARCHITECTURE.md` - How the code is structured and why
3. `docs/SCOPE.md` - The MVP feature list and what is explicitly cut


## License

TBD. Code will be MIT or Apache 2.0. Assets will be original or appropriately licensed (no AoE assets, ever).
