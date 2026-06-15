using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Phase B: stocks the carved crypt. Undead (Anomaly shamblers) hold the rooms, a death-knight
    /// boss (a level-20 enemy hero, so it fights with the full hero AI autocast) guards the vault with
    /// the loot and 1-2 inlays, every room gets a brazier for light, and a couple of mimic chests hide
    /// among them. All hostiles are on the Anomaly entities faction.
    /// </summary>
    public class GenStep_CryptPopulate : GenStep
    {
        public override int SeedPart => 779912034;

        public override void Generate(Map map, GenStepParams parms)
        {
            var comp = map.GetComponent<MapComponent_CryptDungeon>();
            if (comp == null || !comp.IsDungeon) return;
            var faction = Faction.OfEntities;

            int chestsLeft = 2;
            int trapsLeft = 3;
            for (int i = 0; i < comp.rooms.Count; i++)
            {
                var room = comp.rooms[i];
                bool isEntrance = i == comp.entranceIndex;
                bool isBoss = i == comp.bossIndex;

                PlaceBrazier(map, room);

                if (isEntrance) continue;   // the party arrives here; keep it clear

                if (isBoss)
                {
                    SpawnBoss(map, room, faction);
                    SpawnGuards(map, room, faction, 2);
                    SpawnVaultLoot(map, room);
                }
                else
                {
                    SpawnGuards(map, room, faction, Rand.RangeInclusive(2, 4));
                    if (chestsLeft > 0 && Rand.Chance(0.6f)) { PlaceChest(map, room); chestsLeft--; }
                    if (trapsLeft > 0 && Rand.Chance(0.6f)) { PlaceTrap(map, room); trapsLeft--; }
                }
            }
        }

        private static void SpawnGuards(Map map, CellRect room, Faction faction, int count)
        {
            string[] kinds = { "ShamblerSoldier", "ShamblerSwarmer" };
            for (int n = 0; n < count; n++)
            {
                var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kinds[Rand.Range(0, kinds.Length)]);
                if (kind == null) continue;
                if (!TryRoomCell(map, room, out var cell)) break;
                try
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));
                    GenSpawn.Spawn(p, cell, map);
                }
                catch (System.Exception e) { Log.Warning($"[RimHeroes.CryptPopulate] shambler spawn failed: {e.Message}"); }
            }
        }

        private static void SpawnBoss(Map map, CellRect room, Faction faction)
        {
            if (!TryRoomCell(map, room, out var cell)) cell = room.CenterCell;
            DungeonBoss.SpawnCryptLord(map, cell, faction);
        }

        private static void SpawnVaultLoot(Map map, CellRect room)
        {
            var c = room.CenterCell;
            Place(map, c, ThingDefOf.Silver, Rand.RangeInclusive(400, 800));
            var med = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineUltratech")
                      ?? DefDatabase<ThingDef>.GetNamedSilentFail("MedicineIndustrial");
            if (med != null) Place(map, c, med, Rand.RangeInclusive(4, 8));
            var drug = DefDatabase<ThingDef>.GetNamedSilentFail("GoJuice")
                       ?? DefDatabase<ThingDef>.GetNamedSilentFail("Penoxycyline");
            if (drug != null) Place(map, c, drug, Rand.RangeInclusive(3, 6));

            // The rare inlay draw is locked inside the reliquary, not scattered loose.
            PlaceReliquary(map, room);
        }

        private static void PlaceReliquary(Map map, CellRect room)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Reliquary");
            if (def == null) return;
            if (!TryRoomCell(map, room, out var cell)) cell = room.CenterCell;
            GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);   // Building_Reliquary stocks itself on spawn
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
