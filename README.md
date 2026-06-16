# RimHeroes — D&D Classes for RimWorld (playtest alpha)

Rare **Hero** pawns bear a D&D class central to their identity: levels 1–20, Vancian spell slots,
death saving throws, an unremovable class vestment with inlay slots, and a retinue of
job-specialized **mim** followers. Heroes delve themed **dungeons** for loot and capstone weapons,
backed by a growing D&D bestiary — some of it wildshape-able.

**Status: playtest alpha.** All systems verified by automated in-game tests. Art is largely
finished: every class has real per-tier vestment and weapon art, the mim castes and dungeon set
have dedicated sprites, and wildshape forms render real Menagerie creatures. The one remaining
borrowed sprite is the **class tome** (a tinted vanilla textbook).

- Design doc: [docs/DESIGN.md](docs/DESIGN.md) · Research dossier: [docs/RESEARCH.md](docs/RESEARCH.md)

## Requirements

- **Harmony**, the listed **DLCs**, and **Vanilla Expanded Framework** (load before RimHeroes).
- **D&D Menagerie** (Mooloh) — a **hard dependency**. Wildshape forms and some dungeon monsters are
  Menagerie creatures, so the mod will not load without it.

## How to playtest

1. Build (or use the committed DLL), make sure the repo is junctioned into `RimWorld/Mods/`.
2. Enable: **Harmony → (DLCs) → Vanilla Expanded Framework → D&D Menagerie → RimHeroes**.
3. New game → scenario **"The Lone Hero"** → pick your pawn → choose a class at landing.
4. Or in any colony: spawn/buy a **class tome** (12 kinds, exotic goods) and have a pawn study it.

### What to try
- **Hero tab** on your hero: level, XP bar, spell slots, features, vestment + inlay slots, mim roster.
- **Casters**: cast from slots, run dry, sleep (short rest = 1 slot back), use the **Long rest**
  toggle gizmo (~12h sleep = all slots). Autocast toggles per spell; hostile cantrips default on.
- **Druid**: wildshape ladder — dire wolf (L2), owlbear (L5), giant elk (L8), and a capstone dragon
  (L20). Forms render as Menagerie creatures. One form at a time.
- **Cleric**: Revivify (L5) raises corpses dead less than a day.
- **Death saves**: heroes collapse at death's door instead of dying — tend them to boost saves.
- **Mims**: walk in at hero levels 3/8/12; die → replaced in 1–3 days; master dies → panic, leave.
- **Inlays**: 3 vestment slots (defense/offense/utility) × lesser/regular/greater, installed by surgery.
- **Dungeons**: a wealth-scaled entrance can appear on your colony map (trickles monsters until you
  delve or destroy it), arrive as a nearby world-tile quest site (guarded), or roll as an incident.
  Nine themed kinds; delve through fog, traps (spot-then-save), a locked reliquary, and a boss vault.
- **Tiers**: Delve / Dungeon / Capstone scale with colony wealth; bosses and reliquaries can drop a
  Heroic Blessing (5%), and exp candy (S/M/L/XL) drops here and from exotic traders.
- **Capstone**: when a hero hits L20, a hooded-stranger quest fires — shelter him through two
  enemy hero-party raids and he marks a capstone dungeon holding your class's Legendary weapon.
- **Enemy heroes**: raids over 2000 points may include one — they drop inlays and (rarely) tomes.

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
| Druid | Full | Thornlash, Cure Wounds, **Wildshape ×4** |
| Wizard | Full | Fire Bolt, Magic Missile, Mage Armor, Fireball |
| Sorcerer | Full | Fire Bolt, Magic Missile, Fireball |
| Bard | Full | Vicious Mockery, Healing Word, Bless |
| Warlock | Full | Eldritch Blast, Hex |

~90 spells across the eight casters (cantrip → 9th), prepared or known per class.

## Known gaps (not yet coded)

- **Class tome art** — the one remaining borrowed sprite (tinted vanilla textbook); all other art is real.
- **Quiller / Wisp mim castes** — art and job defs exist, but no race def, so those L12 unlock
  slots stay empty.
- **L20 capstone mim free-choice** and **mim beds / hero-housing thoughts** — designed, not built.
- **Warlock Pact Magic** — warlock currently uses full-caster slots; short-rest pact recharge isn't
  modeled yet.
- **`techLeakStopper` setting** — declared but unwired (a no-op toggle; remove or implement).
- **Upcasting, spell-choice subclasses, inlay/tome crafting recipes** — trade/loot only for now.

See [docs/DESIGN.md](docs/DESIGN.md) for the full design and rationale.

## Build

```
dotnet build Source/RimHeroes/RimHeroes.csproj -c Release
```

Targets RimWorld 1.6 (net472, Krafs.Rimworld.Ref). Automated test harnesses live under
`Source/RimHeroes/Dev/` and are gated behind launch flags: run with `-quicktest -rh<name>` (e.g.
`-rhsite`, `-rhcapquest`, `-rhheroraid`, `-rhtrickle`, `-rhfullspike`). Each logs
`RESULT ... verdict=PASS/FAIL` to Player.log and exits. They are inert in normal play.
