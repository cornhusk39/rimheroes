# RimHeroes - D&D Classes for RimWorld (playtest alpha)

Rare **Hero** pawns bear a Dungeons and Dragons class central to their identity: levels 1 to 20,
Vancian spell slots, death saving throws, an unremovable class vestment with inlay slots, and a
retinue of job-specialized **mim** followers. Heroes delve themed **dungeons** for loot and capstone
weapons, backed by a growing D&D bestiary, some of it wildshape-able.

**Status: playtest alpha.** Every system is verified by automated in-game tests and the mod builds
clean. Art is largely finished: every class has real per-tier vestment and weapon art, the mim castes
and dungeon set have dedicated sprites, and wildshape forms render real Menagerie creatures. The one
borrowed sprite left is the **class tome** (a tinted vanilla textbook). Balance numbers are still being
tuned through play.

Design doc: [docs/DESIGN.md](docs/DESIGN.md). Research notes: [docs/RESEARCH.md](docs/RESEARCH.md).

## Requirements

- **Harmony**, the listed **DLCs**, and **Vanilla Expanded Framework** (load before RimHeroes).
- **D&D Menagerie** by Mooloh, a **hard dependency**. Wildshape forms and several dungeon monsters are
  Menagerie creatures, so the mod will not load without it. Grab it on the
  [Steam Workshop](https://steamcommunity.com/sharedfiles/filedetails/?id=2751849453).

## How to play

1. Build (or use the committed DLL) and junction the repo into `RimWorld/Mods/`.
2. Enable in order: **Harmony, (DLCs), Vanilla Expanded Framework, D&D Menagerie, RimHeroes**.
3. New game, scenario **"The Lone Hero"**, pick your pawn, choose a class at landing.
4. Or in any colony, get a **class tome** (12 kinds, sold by exotic goods traders) and have a pawn study it.

### What to try

- **Hero tab** on a hero: level, XP bar, spell slots, features, vestment plus inlay slots, mim roster.
- **Casters**: cast from slots, run dry, sleep (a short rest gives 1 slot back), or use the **Long rest**
  toggle gizmo (about 12h sleep returns all slots). Autocast toggles per spell; hostile cantrips default on.
- **Druid**: the wildshape ladder, dire wolf (L2), owlbear (L5), giant elk (L8), and a capstone dragon
  (L20). Forms render as Menagerie creatures, one at a time. Drop to zero while shifted and you revert,
  hurt but alive.
- **Cleric**: Revivify (L5) raises a corpse dead for less than a day.
- **Death saves**: heroes collapse at death's door instead of dying outright. Tend them to boost the saves.
- **Mims**: walk in at hero levels 3/8/12 (work castes plus combat castes: a melee scrapper, a tank, the
  ranged quiller, the spellcasting wisp). At level 20 the hero calls one extra caste of their choosing.
  Lose one and a replacement arrives within 1 to 3 days; lose the master and the rest panic and leave.
- **Warlock**: pact magic, a few spell slots at one rising level that all return on a short rest (sleep)
  instead of only on a long rest.
- **Inlays**: 3 vestment slots (defense, offense, utility) in lesser, regular, and greater grades, installed by surgery.
- **Dungeons**: a wealth-scaled entrance can open on your colony map (trickling monsters until you delve
  or destroy it), arrive as a guarded world-tile quest site, or roll as an incident. Nine themed kinds,
  each with fog, spot-then-save traps, a locked reliquary, and a boss vault.
- **Tiers**: Delve, Dungeon, and Capstone scale with colony wealth. Bosses and reliquaries can drop a
  Heroic Blessing (5%), and experience candy (S/M/L/XL) drops here and from exotic traders.
- **Capstone**: when a hero hits L20, a hooded-stranger quest fires. Shelter him through two enemy
  hero-party raids and he marks a capstone dungeon holding your class's Legendary weapon.
- **Enemy heroes**: raids over 2000 points may include one. They drop inlays and, rarely, class tomes.

## The 12 classes

| Class | Casting | Signature |
|---|---|---|
| Fighter | None | Heroic Vigor, Battle-Hardened |
| Barbarian | None | Battle Fury (more damage out, less in) |
| Monk | None | Unarmored Swiftness, Flurry of Blows |
| Rogue | None | Cunning Edge, Evasion |
| Ranger | Half | Survivalist, Hunter's Mark, Cure Wounds |
| Paladin | Half | Divine Grit, Lay on Hands, Bless |
| Cleric | Full | Sacred Flame, Cure Wounds, Bless, **Revivify** |
| Druid | Full | Thornlash, Cure Wounds, **Wildshape x4** |
| Wizard | Full | Fire Bolt, Magic Missile, Mage Armor, Fireball |
| Sorcerer | Full | Fire Bolt, Magic Missile, Fireball |
| Bard | Full | Vicious Mockery, Healing Word, Bless |
| Warlock | Full | Eldritch Blast, Hex |

Around 90 spells across the eight casting classes (cantrip up to 9th level), prepared or known per class,
plus a couple dozen martial and class abilities.

## Roadmap and known limitations

**Coming soon**

- **Subclasses.** Each class is planned to branch into Dungeons and Dragons style subclasses with their
  own features and spell options. They are not in yet, so every class currently plays as its base version.

**Known gaps**

- **Class tome art** is the one borrowed sprite left, a tinted vanilla textbook. Everything else is custom.
- **Balance is provisional.** The level curve, damage values, and most tuning numbers are placeholders being
  adjusted through playtest (see `Source/RimHeroes/Heroes/RH_Tuning.cs` and DESIGN.md).
- **Mim housing.** Heroes carry a retinue mood, but dedicated mim-bed furniture and room rules are future work.
- **No crafting recipes** for inlays or tomes yet (trade and loot only), and no spell upcasting.

See [docs/DESIGN.md](docs/DESIGN.md) for the full design and rationale.

## Build

```
dotnet build Source/RimHeroes/RimHeroes.csproj -c Release
```

Targets RimWorld 1.6 (net472, Krafs.Rimworld.Ref). Automated test harnesses live under
`Source/RimHeroes/Dev/` and are gated behind launch flags: run with `-quicktest -rh<name>` (for example
`-rhsite`, `-rhcapquest`, `-rhheroraid`, `-rhtrickle`). Each one spawns a scenario, logs
`RESULT ... verdict=PASS/FAIL` to Player.log, and exits. They stay inert in normal play.

## Credits and legal

- Monsters and wildshape beasts come from [Mooloh's D&D Menagerie](https://steamcommunity.com/sharedfiles/filedetails/?id=2751849453).
  This mod genuinely would not exist without it, so go give them a thumbs up.
- Built on **Harmony** by Andreas Pardeike and **Vanilla Expanded Framework** by Oskar Potocki and the
  Vanilla Expanded team. RimWorld by Ludeon Studios.
- Dungeons and Dragons is a trademark of Wizards of the Coast. RimHeroes is an unofficial, fan-made
  tribute and is not affiliated with or endorsed by Wizards of the Coast.
