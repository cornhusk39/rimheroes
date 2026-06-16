using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Opens a dungeon entrance somewhere on the player's map. Rolls a weighted-random DungeonKind,
    /// stamps it on the entrance (a Building_DungeonEntrance, which the generic GenSteps read while
    /// building the pocket map), and sends a themed letter. The party delves through the MapPortal.
    /// </summary>
    public class IncidentWorker_DungeonEntrance : IncidentWorker
    {
        private const string EntranceDefName = "RH_DungeonEntrance";

        public override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms)
                   && parms.target is Map map
                   && DefDatabase<DungeonKindDef>.AllDefs.Any()
                   && TryFindCell(map, out _);
        }

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map)) return false;
            if (!TryFindCell(map, out var cell)) return false;
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(EntranceDefName);
            if (def == null) return false;

            var kind = DefDatabase<DungeonKindDef>.AllDefs
                .RandomElementByWeightWithFallback(k => k.incidentCommonality);
            if (kind == null) return false;

            var entrance = (Building_DungeonEntrance)ThingMaker.MakeThing(def);
            entrance.kind = kind;
            GenSpawn.Spawn(entrance, cell, map);

            string label = kind.entranceLabel.NullOrEmpty() ? def.LabelCap.ToString() : kind.entranceLabel;
            string text = kind.entranceText.NullOrEmpty() ? def.description : kind.entranceText;
            SendStandardLetter(new TaggedString(label), new TaggedString(text), LetterDefOf.NeutralEvent, parms, entrance);
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
