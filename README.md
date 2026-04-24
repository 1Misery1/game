# 2D Anime Roguelike Action Game

A 2D top-down roguelike action game built with **Unity 2022.3 LTS** (URP 2D) and **Blender** for art production.

## Core Loop

Select Hero → Enter Dungeon → Combat / Collect Weapons / Choose Talents & Buffs → Fight Boss → Clear / Die → Unlock New Heroes → Restart Run

Full design: see [`GDD.docx`](GDD.docx).

## Tech Stack

| Layer | Tool |
|---|---|
| Engine | Unity 2022.3.44f1 (URP 2D) |
| Input | Unity Input System |
| Camera | Cinemachine |
| Art | Blender 4.x (3D → rendered sprite sequences) |
| Target | PC + Mobile |

## Project Structure

```
Assets/Scripts/
├─ Data/        ScriptableObjects: Hero, Weapon, Talent, Buff, Skills, Enums
├─ Core/        GameManager, RunState, PersistentState
├─ Combat/      StatModifier, CharacterStats, Health, Cooldown, IDamageable
├─ Player/      PlayerController (Input System driven)
├─ Dungeon/     DungeonGenerator, RoomController (6 room types)
└─ Systems/     ModifierApplier (applies talent/buff/passive to stats)
```

## Architecture Notes

- **Data-driven:** All heroes, weapons, talents, buffs are `ScriptableObject` assets. Designers can tune everything in the Inspector with no code changes.
- **Modifier stack:** `CharacterStats` holds a list of `StatModifier` entries from talents, buffs, weapon upgrades, and hero passives. Recalculation is lazy (on first `Get` after a change). This lets any system inject stat changes without coupling to others.
- **Run vs Persistent split:** `RunState` is cleared on death (per GDD: HP=0 → lose all weapons/talents/buffs). `PersistentState` stores only unlocked heroes + unlock currency — no out-run stat boosts, to preserve roguelike fairness.
- **Cooldowns separated per skill:** Hero active skill and weapon skill each carry their own `Cooldown` struct.
- **Dungeon generator:** Weighted random room pool + forced boss room per floor.

## Setup

1. Install **Unity Hub** and **Unity 2022.3.44f1**
2. Clone this repo
3. In Unity Hub: **Open** → select this folder
4. Unity will install packages from `Packages/manifest.json` on first open (URP 2D, Input System, Cinemachine)
5. Open any scene under `Assets/Scenes/` (to be added)

## Development Roadmap

See the implementation plan discussed in design docs. Short version:

1. W1–W3: Gray-box prototype (movement, attack, one room, one enemy)
2. W3–W6: Combat + weapons + talents + buffs
3. W6–W9: Random dungeon + 6 room types + heroes
4. W9–W12: Blender art pipeline + UI/audio
5. W13+: Balance, polish, build
