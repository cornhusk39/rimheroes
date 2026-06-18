using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Dev convenience: launch with -quicktest -rhdruid to drop into a normal, playable map with one
    /// starting colonist already a level-3 Druid (vestment + staff + L1-3 grants). Runs once on map
    /// load, then goes inert so the game keeps running for hands-on play.
    /// </summary>
    public class GameComponent_DruidPlay : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhdruid");
        private bool done;

        public GameComponent_DruidPlay(Game game) { }

        public override void GameComponentTick()
        {
            if (!Active || done)
            {
                return;
            }
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 200)
            {
                return; // let the map settle and colonists spawn first
            }
            var pawn = map.mapPawns.FreeColonistsSpawned
                .FirstOrDefault(p => p.RaceProps.Humanlike && !HeroUtility.IsHero(p));
            if (pawn == null)
            {
                pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                GenSpawn.Spawn(pawn, map.Center, map);
            }
            var cls = DefDatabase<HeroClassDef>.GetNamedSilentFail("RH_Druid");
            if (cls == null)
            {
                done = true;
                return;
            }
            var hero = HeroUtility.MakeHero(pawn, cls);
            HeroUtility.GrantStarterWeapon(pawn, cls);
            hero?.SetLevelDirect(3);
            done = true;
            Messages.Message(pawn.LabelShortCap + " is now a level 3 Druid.", pawn, MessageTypeDefOf.PositiveEvent);
        }
    }
}
