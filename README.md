# RimHeroes — D&D Classes for RimWorld (playtest alpha)

Rare **Hero** pawns bear a D&D class central to their identity: levels 1–20, Vancian spell slots,
death saving throws, an unremovable class vestment with inlay slots, and a retinue of
job-specialized **mim** followers. Plus a growing D&D bestiary — some of it wildshape-able.

**Status: playtest alpha.** All systems verified by automated in-game tests. Placeholder art
everywhere (tinted vanilla textures).

- Design doc: [docs/DESIGN.md](docs/DESIGN.md) · Research dossier: [docs/RESEARCH.md](docs/RESEARCH.md)

## How to playtest

1. Build (or use the committed DLL), make sure the repo is junctioned into `RimWorld/Mods/`.
2. Enable: **Harmony → (DLCs) → Vanilla Expanded Framework → RimHeroes**.
3. New game → scenario **"The Lone Hero"** → pick your pawn → choose a class at landing.
4. Or in any colony: spawn/buy a **class tome** (12 kinds, exotic goods) and have a pawn study it.

### What to try
- **Hero tab** on your hero: level, XP bar, spell slots, features, vestment + inlay slots, mim roster.
- **Casters**: cast from slots, run dry, sleep (short rest = 1 slot back), use the **Long rest**
  toggle gizmo (~12h sleep = all slots). Autocast toggles per spell; hostile cantrips default on.
- **Druid**: wildshape ladder — dire wolf (L2), owlbear (L5), giant elk (L8). One form at a time.
- **Cleric**: Revivify (L5) raises corpses dead less than a day.
- **Death saves**: heroes collapse at death's door instead of dying — tend them to boost saves.
- **Mims**: walk in at hero levels 3/8/12; die → replaced in 1–3 days; master dies → panic, leave.
- **Inlays**: 3 vestment slots (defense/offense/utility) × lesser/regular/greater, installed by surgery.
- **Enemy heroes**: raids over 2000 points may include one — they drop inlays and (rarely) tomes.
- **Bestiary**: owlbear, dire wolf packs, giant elk, ankheg, basilisk spawn wild by biome.

## The 12 classes

| Class | Casting | Signature |
|---|---|---|
| Fighter | — | Heroic Vigor, Battle-Hardened |
| Barbarian | — | Battle Fury (more damage out, less in) |
| Monk | — | Unarmored Swiftness, Flurry of Blows |
| Rogue | — | Cunning Edge, Evasion |
| Ranger | Half | Survivalist, Hunter's Mark, Cure Wounds |
| Paladin | Half | Divine Grit, Lay on Hands, Bless |
| Cleric | Full | Sacred Flame, Cure Wounds, Bless, **Revivify** |
| Druid | Full | Thornlash, Cure Wounds, **Wildshape ×3** |
| Wizard | Full | Fire Bolt, Magic Missile, Mage Armor, Fireball |
| Sorcerer | Full | Fire Bolt, Magic Missile, Fireball |
| Bard | Full | Vicious Mockery, Healing Word, Bless |
| Warlock | Full | Eldritch Blast, Hex |

## Known gaps (deliberate, post-playtest)

- All art is tinted placeholder (mims are rats, owlbears are bears, vestments are flak vests).
- Slinger/Wisp mim castes have no race yet — those unlock slots stay empty.
- L20 capstone mim choice, mim beds/housing rules, hero housing thoughts: not yet.
- Tech leak-stopper setting is a stub; upcasting, spell-choice UI, subclasses: not yet.
- Warlock uses full-caster slots (pact magic later). Inlay/tome crafting recipes: trade/loot only.

## Build

```
dotnet build Source/RimHeroes/RimHeroes.csproj -c Release
```

Targets RimWorld 1.6 (net472, Krafs.Rimworld.Ref). Automated test harnesses: launch with
`-quicktest -rh<name>spike` (spike, herospike, deathspike, bondspike, vestspike, shapespike,
spellspike, fullspike) — each logs `RESULT ... verdict=PASS/FAIL` to Player.log and exits.
