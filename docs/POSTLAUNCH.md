# Post-launch wishlist

Things that explicitly **don't** belong in v1 but are worth keeping a note of for after the game ships. Do not work on these. The discipline of writing them down here is exactly what lets us NOT work on them now.

This file is the destination for "while implementing X I noticed Y could be cool" — log it here and move on.

---

## Three-faction asymmetry (Sovereign / Ironborn / Exiled)

From the archived Emberveil design. Each faction would have a small set of stat modifiers:

- **Sovereign**: +20% structure HP, dual build queues, −15% train speed (fortress builders)
- **Ironborn**: +20% movement, −15% unit HP, War Hunger attrition outside combat (raiders)
- **Exiled**: 1.8× build speed, +10% gather rate, −15% unit HP (nomads)

Why post-launch: v1 is single-civ to keep balance tractable. Adding factions multiplies the playtest surface 9× (3v3 matchups). Land the v1 core, then revisit.

**Source:** `archive/war-of-kings-online/docs/DESIGN_DECISIONS.md`. Not imported into the live design — keep that doc archived.

---

## Animated character sprites (frame cycling on movement)

The commissioned sprites in `assets/sources/sprites/` ship 4–5 frames per character. M3 slice 4 wires only the first frame as a static idle. The Emberveil playbook called for cycling frames every 4 ticks (250ms at 4 Hz) when a unit is moving. Same idea applies here at 20 Hz: cycle frames every ~4 ticks.

Why post-launch: visual polish. The static frame is already a major upgrade from circles.

---

## Player color tinting via shader

The current renderer uses two separate fill colors (P1 blue, P2 red) for primitive shapes. When sprite mode is on, both players share the same sprite — there's no team-color tint yet. The Emberveil playbook had a single sprite + a per-player-color modulation shader so you don't ship 2x assets.

Why post-launch: requires writing a small Godot shader and tuning the modulate value. Skippable for v1 if asymmetric sprites are acceptable.

---

## True isometric rendering

The imported iso-tile pack is, predictably, isometric. Our renderer is top-down. Tiles drawn at axis-aligned grid coordinates look slightly perspective-mismatched. Switching the renderer to use Godot's TileMap with an isometric projection would unlock the full iso-tile pack.

Why post-launch: significant renderer rework, fights with our pixel-per-tile math, affects every selection/click hit-test in the codebase. v1 ships with top-down; iso is a v2 option.
