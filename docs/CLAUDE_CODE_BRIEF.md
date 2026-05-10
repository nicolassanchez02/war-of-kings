# War of Kings - Master Autonomy Brief for Claude Code

Paste this into Claude Code as your first message after you've cloned the repo. It tells you what autonomy you have, what you don't, where to get every asset, how to handle every category of work, and how to keep me (the human director) in the loop without blocking on me.

This brief assumes you have read `CLAUDE.md`, `README.md`, and the five documents under `docs/`. If you haven't, read them now before proceeding.

---

## 1. Mission

Ship **War of Kings**: a small, finished, deterministic 1v1 RTS in the spirit of Age of Empires II. Six month target. Solo human director plus you running autonomously between check-ins.

Definition of success: a stranger downloads it from itch.io, plays a full match against the AI or another human, and has fun. That's the bar.

## 2. The autonomy contract

### You can act without asking on:

- All code in `src/Simulation/` (within the architecture rules)
- All code in `src/Networking/`, `src/AI/`, `src/Presentation/` (renderer side)
- All tests, all the time
- All build scripts, CI workflows, tooling under `scripts/`
- All documentation updates that record decisions you've made
- All asset pipeline work: importers, converters, sprite sheet builders
- Downloading and integrating CC0 assets from the approved sources (see Section 6)
- Refactors that don't change observable behavior and have green tests
- Fixing your own bugs
- Filing GitHub issues for things you notice but shouldn't fix right now
- Running games headlessly to test gameplay, recording the results

### You must ask before:

- Adding any feature not listed in the current milestone in `docs/SCOPE.md`
- Spending money on anything (commissioned art, music, paid asset packs, hosting)
- Touching `Fixed64`, `FixedMath`, `DeterministicRng`, `World.Step`, or the state hash function (these are load-bearing, I want eyes on diffs)
- Changing the tick rate, the lockstep delay, or anything in the networking protocol
- Choosing a name, logo, or art direction for marketing
- Publishing anywhere public (itch.io, Steam, social, etc.)
- Adding a third-party dependency (NuGet package, Godot addon, library)
- Removing a determinism test or weakening a determinism assertion
- Changing the license of the repo or any asset's attribution

### You must never:

- Use Age of Empires assets, models, sprites, music, or sound, directly or "inspired by" close enough to be derivative. Not even temporarily. Not even "for testing."
- Reproduce copyrighted code from AoE2 mods, openage, openempire, or any of those repos, beyond fair-use reference (read their blog posts, write your own implementation)
- Put `float`, `double`, `System.Random`, `DateTime`, `Math.X` in simulation code
- Bypass a failing determinism test
- Commit to main without a green CI run
- Generate AI music or AI art and ship it without me reviewing the output and the licensing implications

## 3. Working rhythm

You operate in **sessions**. Each session has a clear scope: usually one item from a milestone checklist in `docs/SCOPE.md`, sometimes a small cluster of related ones.

At the start of every session:
1. Open `CLAUDE.md` and re-orient.
2. Open `docs/SCOPE.md` and identify which milestone you're in and the next unchecked item.
3. Read any session notes from the previous session under `docs/sessions/` (you'll create these).
4. State the goal of this session out loud (write it as the first line of your session note).
5. Work.

At the end of every session:
1. Run the full test suite. Green tests or no commit.
2. Update `docs/SCOPE.md`: check off completed items, add new sub-items if you discovered them.
3. Write a session note at `docs/sessions/YYYY-MM-DD-NN-short-title.md` covering what you did, what's next, and anything I should look at.
4. Commit and push.

A session is over when the goal is met OR you've hit a blocker that needs me. Don't drift past the goal "while you're in there." Scope creep is the enemy.

## 4. The build order (your roadmap, summarized)

This is the same as `docs/SCOPE.md` but condensed so you can hold the whole arc in your head.

**M0: Foundation (week 1-2).** Make the repo build. Implement `Fixed64`, `FixedVector2`, `FixedMath`, `DeterministicRng` fully. Unit tests for all of them. CI green on all three OSes. State hash function working.

**M1: Headless deterministic world (week 3-4).** Grid map, entity factory, one placeholder unit, A* pathing, move commands. The determinism test now exercises real movement.

**M2: Rendering (week 5-6).** Godot scene that opens, camera pan/zoom, terrain tiles drawn from the grid, units interpolated between ticks, selection and right-click move commands.

**M3: Economy (week 7-9).** The heart of the game. Villager gathers wood/food/gold, walks to drop-off, deposits, returns. Town Center trains villagers. Houses raise population. Build menu and building placement.

**M4: Combat (week 10-11).** Militia trains, units fight, HP/damage/armor work, attack-move, stances. Buildings can be destroyed. Town Center destruction = win.

**M5: Full roster (week 12-14).** All 8 units, all 11 buildings. Age advancement. Unit counters working.

**M6: Fog of war (week 15).** Vision radius, scouted/explored/unknown states, minimap.

**M7: AI opponent (week 16-18).** Rule-based AI that builds, gathers, advances, and attacks. Three difficulty levels.

**M8: Networking (week 19-21).** Lockstep multiplayer over ENet. LAN play. OOS detection. Full match completes without desync.

**M9: Replays (week 22).** Auto-save command log, playback at multiple speeds.

**M10: Polish and ship (week 23-26).** Menus, sound, music, tutorial, builds for Windows/macOS/Linux, itch.io release.

Each milestone has a Definition of Done in `docs/SCOPE.md`. Don't move on until it's met.

## 5. Engine and tooling decisions (locked)

These are settled. Do not re-litigate without raising it as a question.

- **Engine: Godot 4.3+** with C# (Mono). Reasons: free, MIT licensed, no royalties, excellent 2D, good C# support, deterministic when used correctly (you avoid Godot's physics in sim), runs on all three target OSes.
- **Language: C# 12 / .NET 8.** Modern, fast enough, cross-platform.
- **Networking: ENet** (via Godot's built-in `MultiplayerPeer`). Reliable for lockstep. UDP with optional reliability per channel.
- **Build/CI: GitHub Actions.** Already scaffolded in `.github/workflows/ci.yml`.
- **Source control: git + GitHub.**
- **Issue tracking: GitHub Issues.** Use labels: `milestone-M3`, `bug`, `design-question`, `blocker`, `polish`.
- **Test framework: xUnit.** Already wired up.
- **Asset pipeline: custom C# tool under `scripts/asset-import/`.** Reads PNG + JSON metadata, produces Godot SpriteFrames resources. Source files are the source of truth.
- **Data format: JSON for unit/building stats.** Hand-editable, diff-friendly. Loaded into the `Database` at sim startup.

### Tools you'll set up in M0

- `dotnet-format` for C# formatting (config in `.editorconfig`, already present)
- A pre-commit hook that runs `dotnet format` and a quick `dotnet build`
- A `scripts/check-sim-purity.sh` script that greps `src/Simulation/` for forbidden patterns (`using Godot;`, `float `, `double `, `System.Random`, etc.) and fails CI if any are found. Write this in M0.
- A `scripts/run-determinism-check.sh` that runs the determinism tests on the current platform and dumps state hashes for cross-platform comparison

## 6. Asset acquisition plan (this is where most projects die, so it's the longest section)

You will get all v1 assets without me writing a check. Here's how.

### 6.1 Visual assets: starting from Kenney CC0 packs

Primary source: **Kenney.nl Medieval RTS pack** (CC0, 120 assets, https://kenney.nl/assets/medieval-rts). This pack has units, structures, environment objects, and tiles. It's purpose-built for exactly this kind of game.

Supplementary Kenney packs (all CC0):
- Isometric Landscape (terrain)
- Isometric Tiles Buildings
- UI Pack (for menus, buttons, panels)

Additional sources if Kenney doesn't cover something:
- **OpenGameArt.org** filtered to CC0 only. Search "medieval RTS", "medieval strategy". Note: many OGA assets are CC-BY-SA which infects your code license. Prefer CC0. If you must use CC-BY, track attribution carefully.
- **itch.io free assets** filtered to "free to use commercially". Read each license.

**The pipeline you'll build (M0-M2):**

1. Create `assets/sources/` directory. Each source pack lives here in its original form with original `LICENSE.txt` and `README.txt` preserved.
2. Create `scripts/asset-import/` tool. Takes a `manifest.json` describing which sprites from which pack map to which game entity (e.g. "kenney_medievalRTSpack/PNG/Units/unit_knight.png" -> "knight").
3. The tool produces `assets/imported/` with renamed, possibly recolored, possibly resized sprites and matching JSON metadata.
4. Godot reads from `assets/imported/`. Source files are in git, imported files are in `.gitignore` (regenerated by the tool).
5. Maintain `assets/CREDITS.md` listing every asset, source, author, and license. Update it whenever you add an asset. This goes in the final build's credits screen.

**Player colors.** Kenney sprites often come in multiple recolored variants or with a clear team-colorable region. Build a sprite tinting shader in the renderer so the same sprite renders in different player colors. This means you don't need 8 versions of every unit sprite.

### 6.2 Audio: sound effects

Primary sources for SFX:
- **OpenGameArt.org Medieval Sound Effects** packs (search CC0 first, CC-BY second). https://opengameart.org/content/medieval-sound-effects-weapon-textures is a CC0 weapon impacts library, perfect for combat.
- **Freesound.org** with CC0 filter. Sword clashes, footsteps, building construction, ambient nature.
- **Kenney UI Audio** (CC0) for menu clicks and notification sounds.

What you need (target inventory for v1):
- 2-3 sword hit variations
- 2-3 arrow loose + impact pairs
- 1-2 villager work sounds (chopping wood, mining, farming)
- Building construction (hammers)
- Building destruction (crash)
- Unit selection acknowledgment (one short voice or click per unit type)
- Unit command acknowledgment (move, attack)
- Resource deposit sound
- Age advancement fanfare
- Victory/defeat stingers
- UI clicks
- Menu hover

Pipeline: same as visual. `assets/sources/audio/` for originals, `assets/imported/audio/` (gitignored) generated. Normalize loudness with `ffmpeg-normalize` so nothing is louder than -16 LUFS.

### 6.3 Audio: music

Music is the hardest to find good free assets for. Three viable paths, in order of preference:

1. **Hire one composer for a small commission.** $300-800 for 2-3 ambient medieval tracks. **Requires my approval** before spending.
2. **CC-BY music from artists like Kevin MacLeod (incompetech.com), Eric Matyas (soundimage.org), or OpenGameArt.org music section.** Attribution required, but doable. Search "medieval ambient", "fantasy strategy", "celtic instrumental". Test each track for "doesn't fight the game" - background music should be tasteful, not insistent.
3. **AI-generated music (e.g. Suno, Udio).** Cheap and abundant. Legal status of AI-generated music is murky and varies by tool. **Requires my approval** of both the tracks and the legal stance before shipping.

Default plan: start with option 2, present me with 5-10 candidate tracks during M3 or M4, I pick 2-3. If I'm not happy with the result during M10, we revisit option 1.

### 6.4 Fonts

Use **Google Fonts** with SIL Open Font License (OFL). They're free for commercial use, redistribute fine. Good medieval-feeling candidates: "IM Fell English", "Cinzel", "MedievalSharp", "UnifrakturMaguntia". Pair with a clean sans for UI (Inter, Source Sans, Lato).

Don't use Windows or macOS system fonts in shipped builds. They're not licensed for redistribution.

### 6.5 Map: the one map for v1

We ship with one handcrafted map, mirrored for 1v1 fairness. Build it in a custom in-engine map editor or via a JSON file. Don't build a full scenario editor for v1.

Map name: "Crossroads" (a working title; I'll bikeshed later). Size: 200x200 tiles. Two starting positions diagonally opposite. Symmetric resource placement.

## 7. Code generation strategy

You will generate a lot of code. Some patterns to keep your output good:

### When implementing a new system

1. Read the relevant section of `docs/ARCHITECTURE.md` first.
2. Write the public API (interfaces, method signatures, data shapes) before the implementation. Show it to me if it touches multiple systems.
3. Write a failing test that demonstrates the desired behavior.
4. Implement until the test passes.
5. Add a determinism test that runs the system twice and asserts identical state hashes.
6. Add an entry to the appropriate doc explaining how the system works.

### When implementing a new unit or building

You have a pattern. Follow it.

1. Add the entry to `assets/data/units.json` or `buildings.json` with reasonable starting stats.
2. Add a sprite mapping in `scripts/asset-import/manifest.json`.
3. If the unit has a new behavior (e.g. a healer that heals other units), implement the behavior in the appropriate System under `src/Simulation/Systems/`.
4. Add a smoke test that creates the unit, exercises its main ability, and asserts the expected outcome.
5. Add a determinism test exercising it.

### When you're unsure

If a design decision could go two ways and the docs don't say, do this:

1. Pick the option that's smaller, simpler, less novel.
2. Write a note in your session log explaining the choice and the alternative.
3. Flag it for my review in the session summary.
4. Move on.

Don't stop and wait unless the decision is truly load-bearing.

## 8. Quality bars

Before you mark a milestone item "done", these must all be true:

- All tests pass on Windows, Linux, and macOS in CI.
- The determinism test for any new system passes.
- No new `using Godot;` in `src/Simulation/`.
- No new floats/doubles/Math.X/Random/DateTime in simulation code (the purity check script catches this).
- New code has at least one test exercising it.
- Documentation reflects what was built.
- If a new file references an asset, that asset is listed in `assets/CREDITS.md` with its license.

A milestone is "done" when every checkbox under it in `docs/SCOPE.md` is checked AND a human (me) has played the game and confirmed the milestone's goal is met in practice, not just on paper.

## 9. Failure modes to watch for in yourself

You will fall into one of these. They're the standard collaborator failure modes. Catch yourself.

- **Scope creep.** "While I was in there I also added X." Don't. File an issue if X is worth doing later.
- **Premature abstraction.** Building a flexible system for a problem you have once. Pick the concrete simple version. We can generalize when there's a second concrete case.
- **Reverting a deliberate decision because you didn't read the doc.** Read the doc before editing things.
- **Test theater.** Writing tests that pass trivially. Read your own assertions out loud. Does that assertion actually fail when the code is broken? If unsure, briefly break the code and confirm the test goes red.
- **Premature optimization.** Building a flow field before A* is profiled and shown insufficient. Don't.
- **Ignoring failing tests.** A red test is the loudest possible signal. Fix it before you do anything else.
- **Floating into renderer-coupled sim code.** If you find yourself needing to know what's on screen to decide game logic, stop. The game logic decides; the renderer reflects.
- **Drifting in a long session.** If you've been working on something for over two hours and it's not converging, stop. Write a session note. End the session. Come back fresh.

## 10. Things you must verify with me, not assume

I want explicit answers on these before you do them. They're not in this brief because they depend on context I have and you don't:

- The actual project name on itch.io. "War of Kings" is the working title.
- The repo's eventual public license. Currently TBD. Defaults to MIT for code, CC0 for original assets, original licenses for third-party assets.
- Whether we'll have any voice-over (unit selection chatter, narrator). Default: no voice acting in v1.
- Whether the game ships with the tutorial in v1 or post-launch. Defaults to v1 per SCOPE.md, but cut if M10 is tight.
- The price (free vs paid on itch.io). Default: free, with optional pay-what-you-want.
- Whether to do a public devlog. Default: no, but record private session notes for our use.

Don't act on assumptions for these. Ask.

## 11. How to ask me things efficiently

When you need my input, batch your questions in the session summary. Don't interrupt for one question if three more are coming an hour later.

Structure each question as:

```
## Question N: [short title]
Context: [one paragraph]
Options: [A, B, C with brief pros/cons]
Recommendation: [your pick + why]
Blocking: [yes/no - can you proceed without an answer?]
```

If I haven't answered and it's not blocking, take your recommendation and proceed. Flag the assumption in the session note. I'll correct it if needed.

## 12. The first session

When I say "go", your first session is M0 setup. Specific tasks, in order:

1. Verify the repo builds (`dotnet build WarOfKings.sln`).
2. Run existing tests, confirm they pass.
3. Implement `FixedMath.Sqrt` properly (Newton's method on the raw long, deterministic, with tests covering 0, 1, 2, large values, fractions, edge cases). Pin the canonical output values in tests.
4. Implement `FixedMath.Sin` and `FixedMath.Cos` (polynomial approximation, deterministic, tests against known values like Sin(0)=0, Sin(pi/2)~1, Sin(pi)~0). Document the precision target (1/65536 = 0.0000153 absolute error).
5. Implement `FixedMath.Atan2` (lookup table or polynomial, deterministic).
6. Fill in the canonical RNG sequence in `DeterministicRngTests.KnownSequence_IsStable`. Run the test, paste the actual values, commit. This is the canonical reference forever.
7. Implement `World.ComputeStateHash` using xxHash64 (or FNV-1a if you prefer no dependency). Add a hash mixing helper.
8. Write `scripts/check-sim-purity.sh` that scans `src/Simulation/` for forbidden patterns and exits non-zero if any are found. Wire it into CI.
9. Push, CI runs, CI is green.
10. Write the session note. End session.

That's your first session. Estimate 4-8 hours of agent time. The output is a foundation that the next 25 weeks build on top of.

## 13. The deal

You are smart and tireless. I am the taste, the priorities, and the final call on contested decisions. You ship a lot of code. I ship a lot of judgment. Between us we ship the game.

Don't be too cautious. Don't be reckless. Read the docs. Write the tests. Push commits I can review.

If you're ever unsure whether to do something, ask one of these three questions and act on the answer:

1. **Is this on the current milestone's checklist?** If yes, do it. If no, file it as an issue for later.
2. **Could this break determinism?** If yes, slow down and write the determinism test first.
3. **Would a stranger downloading the game in six months care about this?** If yes, prioritize. If no, deprioritize.

That's the whole brief. Let's go.

---

## Appendix A: Approved asset sources (quick reference)

| Source | License | Use for |
|--------|---------|---------|
| kenney.nl | CC0 | Sprites, tiles, UI, audio - primary source |
| opengameart.org (CC0 filter) | CC0 | Supplementary sprites and audio |
| opengameart.org (CC-BY filter) | CC-BY | Last resort, tracks attribution |
| freesound.org (CC0 filter) | CC0 | SFX |
| incompetech.com | CC-BY (mostly) | Music, attribution required |
| soundimage.org | Custom permissive | Music, attribution required |
| Google Fonts (OFL) | OFL | All fonts |

Never use: AoE assets, ripped game assets, anything labeled "free for non-commercial only" if we plan to monetize, anything without a clear license file.

## Appendix B: Decisions I want to revisit if and when M9 ships

- A second civilization (likely "Northern" vs "Southern" with different unit speeds and costs)
- A second map
- A campaign mode (3-5 missions)
- Public devlog and Steam page

Don't work on these. They're listed so I can stop thinking about them.

## Appendix C: Anti-goals

These are things we are specifically NOT trying to do. If you find yourself drifting toward any of them, stop.

- We are not making a definitive Age of Empires clone. We're making a small RTS that captures the feel.
- We are not making a moddable platform. Maybe later. For v1, the game is the game.
- We are not making a free-to-play live service. It ships, it's done.
- We are not making the most graphically impressive RTS. We're making the most fun small RTS in our category.
- We are not making a 4X or grand strategy. 1v1 skirmish. 25 minutes per match.

When in doubt: small, finished, polished, deterministic, fun. In that order.
