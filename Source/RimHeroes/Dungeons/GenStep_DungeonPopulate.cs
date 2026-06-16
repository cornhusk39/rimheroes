using System.Collections.Generic;
using System.Linq;
using RimWorld;
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

            int chestsLeft = kind.chestCount;
            int trapsLeft = kind.useTraps ? kind.trapCount : 0;
            for (int i = 0; i < comp.rooms.Count; i++)
            {
                var room = comp.rooms[i];
                bool isEntrance = i == comp.entranceIndex;
                bool isBoss = i == comp.bossIndex;

                PlaceBrazier(map, room);
                if (isEntrance) continue;   // the party arrives here; keep it clear

                if (isBoss)
                {
                    if (TryRoomCell(map, room, out var bossCell) || (bossCell = room.CenterCell) != IntVec3.Invalid)
                        DungeonBoss.Spawn(map, bossCell, faction, kind.boss);
                    SpawnMonsters(map, room, faction, kind, kind.bossGuards);
                    SpawnVaultLoot(map, room, kind);
                }
                else
                {
                    SpawnMonsters(map, room, faction, kind, kind.perRoomMonsters.RandomInRange);
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

        private static void SpawnVaultLoot(Map map, CellRect room, DungeonKindDef kind)
        {
            var c = room.CenterCell;
            Place(map, c, ThingDefOf.Silver, Rand.RangeInclusive(400, 800));
            var med = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineUltratech")
                      ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineIndustrial");
            if (med != null) Place(map, c, med, Rand.RangeInclusive(4, 8));

            // kind-specific extra loot (components for constructs, hides for beasts, etc.)
            if (kind.vaultLoot != null)
                foreach (var loot in kind.vaultLoot)
                    if (loot.thing != null && Rand.Chance(loot.chance))
                        Place(map, c, loot.thing, loot.count.RandomInRange);

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

        private static void PlaceBrazier(Map map, CellRect room)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Brazier");
            if (def == null) return;
            if (TryRoomCell(map, room, out var cell))
                GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
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
