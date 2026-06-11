# RimHeroes — D&D Classes for RimWorld (WIP)

Rare **Hero** pawns bear a D&D class central to their identity: levels 1–20, Vancian spell slots,
death saving throws, an unremovable class vestment that evolves with level, and a retinue of
job-specialized **gestral** followers.

**Status: pre-alpha scaffolding. Not playable.**

- Design doc: [docs/DESIGN.md](docs/DESIGN.md)
- Research dossier (RimWorld 1.6 modding state, June 2026): [docs/RESEARCH.md](docs/RESEARCH.md)

## Targets

- RimWorld **1.6** (1.6.4850), no DLC required (DLC-enhanced where present)
- Hard dependencies: [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077),
  [Vanilla Expanded Framework](https://steamcommunity.com/sharedfiles/filedetails/?id=2023507013)
- Recommended companions: Medieval Overhaul + World Tech Level

## Build

```
dotnet build Source/RimHeroes/RimHeroes.csproj -c Release
```

Outputs `1.6/Assemblies/RimHeroes.dll`. Uses `Krafs.Rimworld.Ref` — no local RimWorld install needed to compile.
To test in-game, clone/junction this folder into `RimWorld/Mods/`.

## Layout

```
About/            mod metadata
1.6/Defs/         XML defs (classes, gestral jobs, traits, hediffs)
1.6/Assemblies/   build output (committed)
Source/RimHeroes/ C# (net472)
docs/             design + research
```

## Next up

1. **Gestral work-AI spike** — mech-work-fields vs custom ThinkTree (gates Sweeper/Hearth/Digger/Sprout/Salve)
2. Fighter vertical slice: vestment, death saves, Porter/Bulwark/Scrapper, devotion/housing
3. Spell engine: slots/rests/autocast via the Wizard
