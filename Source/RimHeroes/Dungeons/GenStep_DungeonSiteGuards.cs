using System.Collections.Generic;
using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimHeroes
{
    /// <summary>
    /// Builds a world-tile dungeon site: drops the dungeon entrance on a clear patch and rings it with a
    /// guard of that dungeon's monsters (scaled by tier/difficulty), set to defend the spot. The party
    /// caravans in, fights the guard, then delves the entrance as normal. Reads the kind off the site's
    /// WorldObjectComp_DungeonSite. Linked to the RH_DungeonSite site part.
    /// </summary>
    public class GenStep_DungeonSiteGuards : GenStep
    {
        public override int SeedPart => 626140087;

        public override void Generate(Map map, GenStepParams parms)
        {
            var comp = (map.Parent as WorldObject)?.GetComponent<WorldObjectComp_DungeonSite>();
            var kind = comp?.kind ?? DefDatabase<DungeonKindDef>.GetNamedSilentFail("RH_Dungeon_Crypt");
            if (kind == null) return;
            Populate(map, kind, comp?.tier ?? 1, comp?.difficulty ?? 1f);
        }

        /// <summary>Drop the entrance + guard force on a site map. Split out so the dev spike can drive
        /// it directly on a plain map without standing up a full world site.</summary>
        public static void Populate(Map map, DungeonKindDef kind, int tier, float difficulty)
        {
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 14, c => EntranceSpotOk(c, map), out var eCell))
                eCell = map.Center;

            var entDef = DefDatabase<ThingDef>.GetNamedSilentFail("RH_DungeonEntrance");
            if (entDef != null)
            {
                foreach (var c in CellRect.CenteredOn(eCell, 3, 3).Cells.Where(c => c.InBounds(map)))
                    foreach (var t in c.GetThingList(map).ToList())
                        if (t.def.destroyable && !(t is Pawn)) t.Destroy(DestroyMode.Vanish);
                var entrance = (Building_DungeonEntrance)ThingMaker.MakeThing(entDef);
                entrance.kind = kind;
                entrance.tier = tier;
                entrance.difficulty = difficulty;
                GenSpawn.Spawn(entrance, eCell, map);
            }

            var faction = Faction.OfEntities;
            int n = 4 + tier * 2 + Mathf.RoundToInt(difficulty);
            var guards = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var mk = kind.RandomMonster();
                if (mk == null) break;
                if (!CellFinder.TryFindRandomCellNear(eCell, map, 9,
                        c => c.Standable(map) && c.GetEdifice(map) == null && !c.Fogged(map), out var gc)) break;
                try
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(mk, faction, forceGenerateNewPawn: true));
                    GenSpawn.Spawn(p, gc, map);
                    guards.Add(p);
                }
                catch (System.Exception e) { Log.Warning($"[RimHeroes.DungeonSite] guard spawn failed: {e.Message}"); }
            }
            if (guards.Count > 0)
            {
                try { LordMaker.MakeNewLord(faction, new LordJob_DefendPoint(eCell), map, guards); }
                catch (System.Exception e) { Log.Warning($"[RimHeroes.DungeonSite] guard lord failed: {e.Message}"); }
            }
        }

        private static bool EntranceSpotOk(IntVec3 c, Map map)
        {
            var rect = CellRect.CenteredOn(c, 3, 3);
            if (!rect.InBounds(map)) return false;
            foreach (var cc in rect)
                if (!cc.Standable(map) || cc.Roofed(map) || cc.GetEdifice(map) != null) return false;
            return true;
        }
    }
}
