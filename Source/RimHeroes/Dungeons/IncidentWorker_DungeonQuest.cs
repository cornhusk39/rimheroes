using System.Linq;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Word reaches the colony of a dungeon out in the world: spawns a visitable Site on a nearby tile,
    /// themed to a wealth-appropriate DungeonKind. The site map (built by GenStep_DungeonSiteGuards)
    /// holds the entrance ringed by a guard of that dungeon's monsters. Caravan there to clear it and
    /// delve. Mirrors the colony-map incident's tier/difficulty roll.
    /// </summary>
    public class IncidentWorker_DungeonQuest : IncidentWorker
    {
        public override bool CanFireNowSub(IncidentParms parms)
        {
            return base.CanFireNowSub(parms)
                   && parms.target is Map
                   && DefDatabase<DungeonKindDef>.AllDefs.Any()
                   && TileFinder.TryFindNewSiteTile(out _);
        }

        public override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map)) return false;
            if (!TileFinder.TryFindNewSiteTile(out var tile)) return false;

            float points = DungeonTiers.PointsForMap(map);
            int tier = DungeonTiers.TierForPoints(points);
            float difficulty = DungeonTiers.DifficultyForPoints(points);

            var kind = DefDatabase<DungeonKindDef>.AllDefs.Where(k => k.AllowedAtTier(tier))
                           .RandomElementByWeightWithFallback(k => k.incidentCommonality)
                       ?? DefDatabase<DungeonKindDef>.AllDefs.RandomElementByWeightWithFallback(k => k.incidentCommonality);
            if (kind == null) return false;

            var part = DefDatabase<SitePartDef>.GetNamedSilentFail("RH_DungeonSite");
            if (part == null) return false;
            var site = SiteMaker.MakeSite(part, tile, null, true);
            if (site == null) return false;
            var comp = site.GetComponent<WorldObjectComp_DungeonSite>();
            if (comp != null) { comp.kind = kind; comp.tier = tier; comp.difficulty = difficulty; }
            Find.WorldObjects.Add(site);

            SendStandardLetter("RH_QuestSiteLabel".Translate(kind.LabelCap),
                "RH_QuestSiteText".Translate(kind.LabelCap),
                LetterDefOf.PositiveEvent, parms, site);
            return true;
        }
    }
}
