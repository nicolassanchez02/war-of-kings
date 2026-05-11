# Asset Credits

This file lists every imported asset pack, its license, author/source, and what it's used for. Update on every import. Keep it synced with each pack's preserved `LICENSE.md` (which lives next to the pack's PNGs).

Repo policy: the war-of-kings repo is private. Commissioned assets that "do not redistribute" are usable here but must not be exposed in a public mirror.

---

## sprites/  — Commissioned character sprites
- **License:** All rights reserved — commissioned, owned by Nicolas Sanchez / Grubwire Studio
- **Source:** Imported from `archive/war-of-kings-online/assets/sprites/` (2026-05-12)
- **Pack LICENSE:** `assets/sources/sprites/LICENSE.md` (the artist credit field is `[fill in]` — populate when known)
- **Size:** 1.1 MB, ~30 character folders × 4-5 frames each, 32×32 RGBA
- **Used by war-of-kings for:**
  - `farmer_01/farmer_01_1.png` → villager (UnitTypeId 1)
  - `guard/guard_1.png` → militia (UnitTypeId 2)
  - Other folders preserved for future passes (villager_01/_02, dwarf, witch, captain, blacksmith, dog, etc.)
- **Notes:**
  - Only the first frame is wired this slice; animation cycling is deferred.
  - These were drawn for a 2x render scale (final on-screen 64×64). Our renderer scales to fit the unit's drawing radius, so the upscale is automatic.
  - The Emberveil project assigned several of these to factions (Sovereign/Ironborn/Exiled). War of Kings is single-civ for v1; faction mapping is explicitly out of scope (see POSTLAUNCH.md).

## buildings/  — Isometric building + structure pack
- **License:** Unclear — `assets/sources/buildings/LICENSE.md` has `[fill in]` placeholders for both Source and License fields
- **Source:** Imported from `archive/war-of-kings-online/assets/buildings/` (2026-05-12). Original download source not recorded.
- **Size:** 389 KB
- **Used by war-of-kings for:**
  - `castle.png` → Town Hall building (BuildingTypeId.TownHall)
  - `wheat-fields/wheat-fenced-1.png` → reserved for berry-patch visual override
  - Remaining files (walls, paths, stone-turrets, windmill, cannon, flag-tower) preserved for future passes (M5+ buildings)
- **Notes:**
  - LICENSE metadata is incomplete in the source pack. **Action item:** Nick should confirm the source + license before this repo ever goes public. Flagged in OPEN_QUESTIONS.

## iso-tiles/  — Isometric Strategy Medieval Pixel Art Tiles
- **License:** Creative Commons Attribution 4.0 International (CC BY 4.0)
- **Source:** itch.io — exact URL not recorded in pack LICENSE (`assets/sources/iso-tiles/License.txt` and `LICENSE.md` are present; URL field is `[fill in exact URL]`)
- **Required attribution:** Must credit the creator in game credits. **Action item:** populate artist name when known.
- **Size:** 1.1 MB, 188 individual tile PNGs (64×64 isometric) + an overview image
- **Used by war-of-kings for:**
  - `individual/forest-1.png` → Tree resource visual
  - Remaining tiles preserved for future passes (terrain, mountain, water, building variants)
- **Notes:**
  - These are **isometric** tiles. Our renderer is **top-down**, so they will look slightly off-perspective when drawn at axis-aligned tile coordinates. They're still a major step up from colored circles. The full visual overhaul to actual isometric rendering is deferred (see OPEN_QUESTIONS).
  - The pack includes Aseprite source files (`Isometric_strategy.aseprite`) — useful if you ever want to recolor or extend.

---

## What's NOT imported (intentional)

- `archive/war-of-kings-online/assets/user-interface/` — 30 MB of HUD panels. The current HUD is text-on-overlay; no UI panels needed yet. Defer to a future HUD polish slice.
- `archive/war-of-kings-online/assets/music/` and `sfx/` — M10 polish, audio not yet wired.
- `archive/war-of-kings-online/assets/npc-sprites/` and `resource-icons/` — not yet needed; the sprites pack covers villagers/militia, and the resource HUD is text-based.
- `archive/war-of-kings-online/assets/iso-tiles-outdoor/` — second iso tile pack; one is enough until terrain rendering moves to true isometric.
- `archive/war-of-kings-online/assets/game assets/` (Midjourney AI art) — that folder is governed by `archive/war-of-kings-online/docs/AI_ART_POLICY.md` which is Emberveil-specific. Out of scope for this import per Nick's constraints.

## What's NOT imported (per Nick's constraints)

- `docs/LORE.md` — Emberveil lore, persistent-world MMO setting. Different game.
- `docs/DESIGN_DECISIONS.md` — Emberveil-specific (seasons, day/night, wolves). Different game.
- `docs/VISUAL_DIRECTION.md` — full doc not imported; only tone-referenced (isometric AoE II look) where relevant. Our actual rendering is top-down; isometric is a future possibility.
- The faction concept (Sovereign / Ironborn / Exiled) — out of scope for v1 single-civ design. Logged in `docs/POSTLAUNCH.md`.
