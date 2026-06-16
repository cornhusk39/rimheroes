using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A crypt loot chest. Select it and hit Open: most chests spill their treasure, but some are
    /// mimics that spring into a monster instead. The mimic is Menagerie's animated tool cabinet (a
    /// chest that comes alive); it falls back to a bound imp if Menagerie is somehow missing.
    /// </summary>
    public class Building_LootChest : Building
    {
        private bool isMimic;
        private bool decided;

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad && !decided)
            {
                isMimic = Rand.Chance(0.33f);
                decided = true;
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            yield return new Command_Action
            {
                defaultLabel = "Open",
                defaultDesc = "Open this chest. Not everything in a crypt is what it seems.",
                action = Open
            };
        }

        private void Open()
        {
            var map = Map;
            var pos = Position;
            if (isMimic)
            {
                SpawnMimic(map, pos);
                Messages.Message("RH_ChestMimic".Translate(), new LookTargets(pos, map), MessageTypeDefOf.ThreatSmall, false);
            }
            else
            {
                SpawnLoot(map, pos);
                Messages.Message("RH_ChestLooted".Translate(), new LookTargets(pos, map), MessageTypeDefOf.PositiveEvent, false);
            }
            Destroy(DestroyMode.Vanish);
        }

        private static void SpawnMimic(Map map, IntVec3 pos)
        {
            var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail("DND_AnimatedToolCabinet")
                       ?? DefDatabase<PawnKindDef>.GetNamedSilentFail("RH_ImpKind");
            if (kind == null) return;
            try
            {
                var faction = Faction.OfEntities ?? Faction.OfMechanoids;
                var mimic = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));
                GenSpawn.Spawn(mimic, pos, map);
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes] mimic spawn failed: {e}"); }
        }

        private static void SpawnLoot(Map map, IntVec3 pos)
        {
            Place(map, pos, ThingDefOf.Silver, Rand.RangeInclusive(120, 320));
            var med = DefDatabase<ThingDef>.GetNamedSilentFail("MedicineIndustrial");
            if (med != null) Place(map, pos, med, Rand.RangeInclusive(2, 5));
        }

        private static void Place(Map map, IntVec3 pos, ThingDef def, int count)
        {
            if (def == null || count <= 0) return;
            var t = ThingMaker.MakeThing(def);
            t.stackCount = count;
            GenPlace.TryPlaceThing(t, pos, map, ThingPlaceMode.Near);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref isMimic, "isMimic");
            Scribe_Values.Look(ref decided, "decided");
        }
    }
}
