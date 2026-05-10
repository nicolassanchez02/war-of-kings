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
# Clone and open in Godot 4.3+
git clone <repo-url>
cd war-of-kings
godot --editor project.godot
```



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
