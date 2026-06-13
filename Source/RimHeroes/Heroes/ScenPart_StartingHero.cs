using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Scenario part: drops a Heroic Blessing beside each starting colonist. Using it opens class
    /// selection and remakes the pawn into a Hero (clean traits, cured body, starter weapon).
    /// </summary>
    public class ScenPart_StartingHero : ScenPart
    {
        public override void PostGameStart()
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_HeroicBlessing");
            if (def == null)
            {
                Log.Warning("[RimHeroes] ScenPart_StartingHero: RH_HeroicBlessing missing.");
                return;
            }
            LongEventHandler.ExecuteWhenFinished(() =>
            {
                var pawns = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?.Where(p => !HeroUtility.IsHero(p)).ToList();
                if (pawns == null || pawns.Count == 0)
                {
                    return;
                }
                foreach (var pawn in pawns)
                {
                    var item = ThingMaker.MakeThing(def);
                    GenPlace.TryPlaceThing(item, pawn.Position, pawn.Map, ThingPlaceMode.Near);
                }
                Find.LetterStack.ReceiveLetter("A heroic blessing",
                    "A mote of impossible light rests beside " +
                    (pawns.Count == 1 ? pawns[0].LabelShortCap.ToString() : "your colonists") +
                    ". Have them receive the Heroic Blessing to awaken as a Hero and choose their path.",
                    LetterDefOf.PositiveEvent, new LookTargets(pawns));
            });
        }

        public override string Summary(Scenario scen) => "RH_ScenPart_StartingHeroSummary".Translate();
    }
}
