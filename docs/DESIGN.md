# Design Document

## One-line pitch

A tight, deterministic 1v1 skirmish RTS where two players race from a small settlement to a small army, won or lost in 15 to 25 minutes.

## Pillars

These are the three things the game must nail. Every design decision either serves one of these or gets cut.

1. **Readable at a glance.** The player should know what every unit is, who owns it, and what it's doing within one second of looking at the screen. Sprite silhouettes matter. Player colors matter. UI clarity matters.

2. **Honest economy loop.** Gathering resources should feel like the heart of the game. Villagers walking to wood, chopping, walking back, depositing. That rhythm is the soul of AoE and we keep it.

3. **Deterministic and fair.** Two players running the same input log must produce the same outcome on any machine. No floating point in the simulation. No platform-dependent math. Replays are byte-identical or we've broken something.

## What the game is

- 1v1 skirmish on a single map size (roughly 200x200 tiles)
- One playable civilization at launch (no asymmetry)
- Two ages: Settlement Age and Castle Age
- Three resources: Wood, Food, Gold
- Lockstep networked multiplayer (LAN first, then internet)
- Skirmish vs AI opponent
- Replays

## What the game is not

- Not a campaign game. No story missions at launch.
- Not multi-civilization. One civ until the core is solid.
- Not a 4-player FFA. 1v1 only at launch.
- Not 3D. Not isometric pre-rendered. Hand-drawn or vector 2D top-down or 2.5D.
- Not a tech tree simulator. Maybe 15 to 20 upgrades total, not 100+.
- Not free-to-play, not live service, not a platform. It's a game. It ends.

## The feel target

The game should feel like the first 15 minutes of an Age of Empires II match. The part where you're booming villagers, scouting, deciding when to advance to the next age, building your first military, and the tension is rising. We capture that arc and end the game roughly when AoE2 would be hitting Imperial. We don't try to extend it.

A match plays in 15 to 25 minutes. Quick to start, quick to lose, quick to rematch.

## Win condition

Destroy the opponent's Town Center. That's it for v1. No wonder victory, no relic victory, no score victory. One condition. It keeps the game readable and the AI tractable.

## Units (target roster for v1)

Settlement Age:
- Villager (gathers, builds, repairs)
- Militia (cheap melee)
- Scout (fast, weak, sight)

Castle Age:
- Spearman (anti-cavalry melee)
- Archer (ranged)
- Knight (heavy cavalry)
- Ram (anti-building)
- Onager (siege, anti-group)

Total: 8 units. Each must feel distinct.

## Buildings (target roster for v1)

Settlement Age:
- Town Center (trains villagers, drop-off for all resources, win condition)
- House (population cap, +5 each)
- Lumber Camp (wood drop-off)
- Mill (food drop-off)
- Mining Camp (gold drop-off)
- Barracks (trains Militia, Spearman)
- Watchtower (defensive)

Castle Age:
- Archery Range (trains Archer)
- Stable (trains Scout, Knight)
- Siege Workshop (trains Ram, Onager)
- Castle (advanced defensive, upgrade options)

Total: 11 buildings.

## The economy loop (the heart of the game)

This is the loop the player spends most of their time on. It must feel good.

```
Villager idle at Town Center
    -> player right-clicks tree
    -> villager walks to tree (pathfinding)
    -> villager chops (animation, wood ticks up on a timer)
    -> villager inventory fills (capacity ~10 wood)
    -> villager walks to nearest drop-off (Town Center or Lumber Camp)
    -> wood added to player's stockpile
    -> villager walks back to the same or nearest tree
    -> repeat until tree depleted, then auto-target nearest tree
```

Same loop for berries/farms (food) and gold mines (gold).

The "auto-target next resource" behavior is critical. Without it the game is tedious.

## Age advancement

- Settlement Age: starting age, basic units and economy
- Advancing requires: a Town Center, 500 food, 30 seconds of research
- Castle Age: unlocks military buildings (Stable, Archery Range, Siege Workshop, Castle) and Castle Age units

Two ages, one transition. That's the whole tech progression arc. Anything more elaborate is post-launch.

## Combat model

- Units have HP, attack damage, attack speed, armor (melee and pierce)
- Damage = max(1, attacker_damage - target_armor)
- No critical hits, no random damage rolls. Determinism trumps surprise.
- Range units have a minimum and maximum range
- Melee units have an attack animation; damage applies on a specific animation frame
- Units have a default behavior: attack enemies in sight unless told otherwise
- Stances: Aggressive (default, chase), Defensive (return to spot after fight), Hold Ground (don't move)

## What we cut (and why)

- **Multiple civilizations.** Doubles or triples balance work. Cut.
- **Naval combat.** Whole second game system. Cut.
- **Walls.** Huge pathfinding complexity and griefing potential. Cut for v1, maybe add later.
- **Gates.** Same as walls.
- **Trade.** Adds a fourth economic mechanic that's hard to balance with three. Cut.
- **Diplomacy.** No-op in 1v1. Cut.
- **Heroes.** Different design pillar. Cut.
- **Random map generation.** One handcrafted map, mirrored. Cut RMS.
- **In-game chat.** Out of scope for v1.
- **Spectator mode.** Out of scope for v1, but replays cover the use case.

If you find yourself wanting to add one of these, write the idea in `docs/POSTLAUNCH.md` and move on.

## Match pacing target

A reference match should look roughly like:

- 0:00 to 2:00 - Initial villager production, scouting, gathering starts
- 2:00 to 6:00 - Economy ramp, first houses, second drop-off buildings
- 6:00 to 10:00 - Decision to advance, first Barracks, possibly first military
- 10:00 to 12:00 - Age advancement complete, military buildings going up
- 12:00 to 20:00 - Skirmishes, raids, eventual decisive engagement
- 20:00 to 25:00 - Game decided, cleanup

If matches consistently run longer than 25 minutes, the economy is too forgiving or the military is too weak. We tune toward this window.

## Aesthetic direction

- Top-down or 2.5D, leaning top-down for readability
- Bold, saturated player colors (blue and red for v1)
- Stylized rather than realistic. Think Battle Brothers, Bad North, or Tooth and Tail rather than AoE2.
- Clean silhouettes. A unit should be identifiable from its outline alone.
- UI: minimal, parchment-and-ink palette, no skeuomorphism

This direction is chosen because it's achievable with a small art budget. We're not competing with AoE2 DE on fidelity, we're competing on feel.
