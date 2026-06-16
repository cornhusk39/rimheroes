using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Stocks a carved dungeon from its DungeonKind: a themed monster pool holds the rooms, the boss
    /// guards the vault with loot + 1-2 inlays (locked in the reliquary), every room gets a brazier for
    /// light, and traps + mimic chests hide among them. All hostiles spawn on the Anomaly entities
    /// faction so they are uniformly hostile regardless of which mod supplied the monster.
    /// </summary>
    public class GenStep_DungeonPopulate : GenStep
    {
        public override int SeedPart => 779912034;

        public override void Generate(Map map, GenStepParams parms)
        {
            var comp = map.GetComponent<MapComponent_Dungeon>();
            if (comp == null || !comp.IsDungeon) return;
            var kind = comp.kind ?? DefDatabase<DungeonKindDef>.GetNamedSilentFail("RH_Dungeon_Crypt");
            if (kind == null) return;
            var faction = Faction.OfEntities;
            float diff = Mathf.Max(0.5f, comp.difficulty);
            int tier = comp.tier;

            int chestsLeft = kind.chestCount;
            int trapsLeft = kind.useTraps ? kind.trapCount : 0;
            for (int i = 0; i < comp.rooms.Count; i++)
            {
                var room = comp.rooms[i];
                bool isEntrance = i == comp.entranceIndex;
                bool isBoss = i == comp.bossIndex;

                PlaceBrazier(map, room, kind);
                if (isEntrance) continue;   // the party arrives here; keep it clear

                PlaceProps(map, room, kind);

                if (isBoss)
                {
                    PlaceSigil(map, room, kind);
                    if (TryRoomCell(map, room, out var bossCell) || (bossCell = room.CenterCell) != IntVec3.Invalid)
                        DungeonBoss.Spawn(map, bossCell, faction, kind);
                    SpawnMonsters(map, room, faction, kind, kind.bossGuards + (tier - 1));
                    SpawnVaultLoot(map, room, kind, tier);
                }
                else
                {
                    SpawnMonsters(map, room, faction, kind, Mathf.RoundToInt(kind.perRoomMonsters.RandomInRange * diff));
                    if (chestsLeft > 0 && Rand.Chance(0.6f)) { PlaceChest(map, room); chestsLeft--; }
                    if (trapsLeft > 0 && Rand.Chance(0.6f)) { PlaceTrap(map, room); trapsLeft--; }
                }
            }
        }

        private static void SpawnMonsters(Map map, CellRect room, Faction faction, DungeonKindDef kind, int count)
        {
            for (int n = 0; n < count; n++)
            {
                var kindDef = kind.RandomMonster();
                if (kindDef == null) break;
                if (!TryRoomCell(map, room, out var cell)) break;
                try
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kindDef, faction, forceGenerateNewPawn: true));
                    GenSpawn.Spawn(p, cell, map);
                }
                catch (System.Exception e) { Log.Warning($"[RimHeroes.DungeonPopulate] {kindDef.defName} spawn failed: {e.Message}"); }
            }
        }

        private static void SpawnVaultLoot(Map map, CellRect room, DungeonKindDef kind, int tier)
        {
            var c = room.CenterCell;
            float lootMult = 1f + 0.5f * (tier - 1);   // tier 2 = 1.5x, tier 3 = 2x
            Place(map, c, ThingDefOf.Silver, Mathf.RoundToInt(Rand.RangeInclusive(400, 800) * lootMult));
            var med = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineUltratech")
                      ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineIndustrial");
            if (med != null) Place(map, c, med, Mathf.RoundToInt(Rand.RangeInclusive(4, 8) * lootMult));

            // kind-specific extra loot (components for constructs, hides for beasts, etc.)
            if (kind.vaultLoot != null)
                foreach (var loot in kind.vaultLoot)
                    if (loot.thing != null && Rand.Chance(loot.chance))
                        Place(map, c, loot.thing, Mathf.RoundToInt(loot.count.RandomInRange * lootMult));

            // rare-but-not-impossible: a chance at exp candy in the vault. Higher tiers favour larger candy.
            if (Rand.Chance(0.35f + 0.1f * (tier - 1)))
            {
                float r = Rand.Value - 0.15f * (tier - 1);   // shift the weights toward bigger candy
                string candy = r < 0.55f ? "RH_ExpCandy_S" : r < 0.83f ? "RH_ExpCandy_M"
                               : r < 0.96f ? "RH_ExpCandy_L" : "RH_ExpCandy_XL";
                Place(map, c, DefDatabase<ThingDef>.GetNamedSilentFail(candy), candy.EndsWith("_S") ? Rand.RangeInclusive(1, 2) : 1);
            }

            // the rare inlay draw is locked inside the reliquary, not scattered loose
            PlaceReliquary(map, room);
        }

        private static void PlaceReliquary(Map map, CellRect room)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Reliquary");
            if (def == null) return;
            if (!TryRoomCell(map, room, out var cell)) cell = room.CenterCell;
            GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
        }

        private static void PlaceBrazier(Map map, CellRect room, DungeonKindDef kind)
        {
            var def = kind?.brazierDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("RH_Brazier");
            if (def == null) return;
            if (TryRoomCell(map, room, out var cell))
                GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
        }

        private static void PlaceProps(Map map, CellRect room, DungeonKindDef kind)
        {
            if (kind?.props.NullOrEmpty() ?? true) return;
            int n = kind.propsPerRoom.RandomInRange;
            for (int i = 0; i < n; i++)
            {
                var def = kind.RandomProp();
                if (def == null) break;
                if (TryRoomCell(map, room, out var cell))
                    GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
            }
        }

        private static void PlaceSigil(Map map, CellRect room, DungeonKindDef kind)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_VaultSigil");
            if (def == null) return;
            var cell = room.CenterCell;
            if (!cell.InBounds(map) || cell.GetEdifice(map) != null)
                if (!TryRoomCell(map, room, out cell)) return;
            var sigil = (Building_VaultSigil)GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
            var c = kind.bossAura.a > 0f ? kind.bossAura : Color.white;
            c.a = 1f;
            sigil.SetTint(c);
        }

        private static void PlaceChest(Map map, CellRect room)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_LootChest");
            if (def == null) return;
            if (TryRoomCell(map, room, out var cell))
                GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
        }

        private static readonly string[] TrapDefs = { "RH_Trap_Blade", "RH_Trap_Dart" };

        private static void PlaceTrap(Map map, CellRect room)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(TrapDefs[Rand.Range(0, TrapDefs.Length)]);
            if (def == null) return;
            if (TryRoomCell(map, room, out var cell))
                GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
        }

        private static bool TryRoomCell(Map map, CellRect room, out IntVec3 cell)
        {
            cell = IntVec3.Invalid;
            var options = room.ContractedBy(1).Cells.Where(c => c.InBounds(map) && c.Standable(map) && c.GetEdifice(map) == null).ToList();
            if (options.Count == 0) return false;
            cell = options.RandomElement();
            return true;
        }

        private static void Place(Map map, IntVec3 pos, ThingDef def, int count)
        {
            if (def == null || count <= 0) return;
            var t = ThingMaker.MakeThing(def);
            t.stackCount = count;
            GenPlace.TryPlaceThing(t, pos, map, ThingPlaceMode.Near);
        }
    }
}
