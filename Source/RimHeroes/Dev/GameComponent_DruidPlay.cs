using System;
using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Dev convenience: launch with <c>-quicktest -rhhero -rhclass=RH_Wizard -rhlevel=20</c> to drop into
    /// a normal, playable map with one starting colonist already a hero of the chosen class and level
    /// (vestment + starter weapon + all grants up to that level). Defaults to a level-1 Fighter. Runs once
    /// on map load, then goes inert so the game keeps running for hands-on play.
    /// </summary>
    public class GameComponent_HeroPlay : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhhero");
        private bool done;

        public GameComponent_HeroPlay(Game game) { }

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
            string className = ArgValue("rhclass") ?? "RH_Fighter";
            int level = int.TryParse(ArgValue("rhlevel"), out int lvl) ? lvl : 1;
            var cls = DefDatabase<HeroClassDef>.GetNamedSilentFail(className);
            if (cls == null)
            {
                done = true;
                return;
            }
            var hero = HeroUtility.MakeHero(pawn, cls);
            HeroUtility.GrantStarterWeapon(pawn, cls);
            hero?.SetLevelDirect(level);
            done = true;
            Messages.Message($"{pawn.LabelShortCap} is now a level {level} {cls.label}.", pawn, MessageTypeDefOf.PositiveEvent);
        }

        // Reads a "-key=value" command-line argument (leading dash optional).
        private static string ArgValue(string key)
        {
            foreach (string a in Environment.GetCommandLineArgs())
            {
                string s = a.StartsWith("-") ? a.Substring(1) : a;
                if (s.StartsWith(key + "="))
                {
                    return s.Substring(key.Length + 1);
                }
            }
            return null;
        }
    }
}
