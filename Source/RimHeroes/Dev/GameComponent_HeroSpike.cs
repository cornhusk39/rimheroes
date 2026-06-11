using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated test: launch with -quicktest -rhherospike. Opens the class picker on the first
    /// colonist, screenshots it (visual check), programmatically confirms Fighter, verifies the
    /// trait + hediff landed, then shuts down. Realtime-driven because the dialog pauses the game.
    /// </summary>
    public class GameComponent_HeroSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhherospike");

        private int state;
        private float nextStateTime = -1f;
        private Pawn pawn;
        private Dialog_ChooseHeroClass dialog;

        public GameComponent_HeroSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 3)
            {
                return;
            }
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f)
            {
                nextStateTime = now + 4f;
                return;
            }
            if (now < nextStateTime)
            {
                return;
            }
            nextStateTime = now + 4f;

            switch (state)
            {
                case 0:
                    pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                    if (pawn == null)
                    {
                        pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(pawn, map.Center, map);
                    }
                    dialog = new Dialog_ChooseHeroClass(pawn);
                    Find.WindowStack.Add(dialog);
                    Log.Message($"[RimHeroes.HeroSpike] dialog opened for {pawn.LabelShort}; classes={DefDatabase<HeroClassDef>.AllDefsListForReading.Count}; screenshots={GenFilePaths.ScreenshotFolderPath}");
                    state = 1;
                    break;
                case 1:
                    ScreenshotTaker.TakeNonSteamShot("rhherospike");
                    state = 2;
                    break;
                case 2:
                {
                    var cls = DefDatabase<HeroClassDef>.GetNamed("RH_Fighter");
                    dialog.Confirm(cls);
                    var hediff = HeroUtility.GetHeroHediff(pawn);
                    bool hasTrait = pawn.story?.traits?.HasTrait(RH_DefOf.RH_Hero) ?? false;
                    bool dialogClosed = !Find.WindowStack.IsOpen<Dialog_ChooseHeroClass>();
                    string verdict = hediff != null && hediff.classDef == cls && hediff.level == 1 && hasTrait && dialogClosed ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.HeroSpike] RESULT: class={hediff?.classDef?.defName ?? "null"} level={hediff?.level ?? -1} trait={hasTrait} dialogClosed={dialogClosed} verdict={verdict}");
                    state = 3;
                    break;
                }
                case 3:
                    state = 4;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
