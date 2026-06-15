using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Opens a crypt entrance somewhere on the player's map. The entrance is a MapPortal the player
    /// sends a party of heroes through to delve the instanced crypt below.
    /// </summary>
    public class IncidentWorker_DungeonEntrance : IncidentWorker
    {
        private const string EntranceDefName = "RH_DungeonEntrance";

        public override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms)
                   && parms.target is Map map
                   && TryFindCell(map, out _);
        }

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map)) return false;
            if (!TryFindCell(map, out var cell)) return false;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(EntranceDefName);
            if (def == null) return false;

            var thing = GenSpawn.Spawn(ThingMaker.MakeThing(def), cell, map);
            SendStandardLetter(parms, thing);
            return true;
        }

        private static bool TryFindCell(Map map, out IntVec3 cell)
        {
            return CellFinderLoose.TryGetRandomCellWith(c =>
            {
                var rect = CellRect.CenteredOn(c, 3, 3);
                if (!rect.InBounds(map)) return false;
                foreach (var cc in rect)
                {
                    if (!cc.Standable(map) || cc.Fogged(map) || cc.GetEdifice(map) != null || cc.Roofed(map)) return false;
                }
                return true;
            }, map, 1000, out cell);
        }
    }
}
