using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Scenario part: after game start, opens the class picker for the starting colonist.
    /// </summary>
    public class ScenPart_StartingHero : ScenPart
    {
        public override void PostGameStart()
        {
            var pawn = Find.CurrentMap?.mapPawns?.FreeColonistsSpawned?.FirstOrDefault()
                       ?? PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_FreeColonists.FirstOrDefault();
            if (pawn == null)
            {
                Log.Warning("[RimHeroes] ScenPart_StartingHero: no starting colonist found.");
                return;
            }
            LongEventHandler.ExecuteWhenFinished(() => Find.WindowStack.Add(new Dialog_ChooseHeroClass(pawn)));
        }

        public override string Summary(Scenario scen) => "RH_ScenPart_StartingHeroSummary".Translate();
    }
}
