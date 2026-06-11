using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated test: launch with -quicktest -rhherospike. Opens the class picker on the first
    /// colonist, screenshots it, programmatically confirms Fighter, verifies trait + hediff +
    /// level-1 grant, pumps XP to verify level-ups apply later grants, screenshots the selected
    /// hero (ITab visible), then shuts down. Realtime-driven because the dialog pauses the game.
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
            if (!Active || state > 5)
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
                    bool hasVigor = pawn.health.hediffSet.HasHediff(HediffDef.Named("RH_Feature_HeroicVigor"));
                    bool dialogClosed = !Find.WindowStack.IsOpen<Dialog_ChooseHeroClass>();
                    Log.Message($"[RimHeroes.HeroSpike] confirm: class={hediff?.classDef?.defName ?? "null"} level={hediff?.level ?? -1} trait={hasTrait} L1grant(vigor)={hasVigor} dialogClosed={dialogClosed}");
                    state = 3;
                    break;
                }
                case 3:
                {
                    var hediff = HeroUtility.GetHeroHediff(pawn);
                    hediff.GainXP(600f); // 100+115+132+152=499 to reach L5
                    bool hasHardened = pawn.health.hediffSet.HasHediff(HediffDef.Named("RH_Feature_BattleHardened"));
                    Log.Message($"[RimHeroes.HeroSpike] xp pump: level={hediff.level} xp={hediff.xp:F0} L5grant(hardened)={hasHardened}");
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(pawn);
                    state = 4;
                    break;
                }
                case 4:
                {
                    ScreenshotTaker.TakeNonSteamShot("rhherospike2");
                    var hediff = HeroUtility.GetHeroHediff(pawn);
                    bool hasTrait = pawn.story?.traits?.HasTrait(RH_DefOf.RH_Hero) ?? false;
                    bool hasVigor = pawn.health.hediffSet.HasHediff(HediffDef.Named("RH_Feature_HeroicVigor"));
                    bool hasHardened = pawn.health.hediffSet.HasHediff(HediffDef.Named("RH_Feature_BattleHardened"));
                    string verdict = hediff != null && hediff.classDef?.defName == "RH_Fighter" && hediff.level == 5
                                     && hasTrait && hasVigor && hasHardened ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.HeroSpike] RESULT: class={hediff?.classDef?.defName} level={hediff?.level} trait={hasTrait} vigor={hasVigor} hardened={hasHardened} verdict={verdict}");
                    state = 5;
                    break;
                }
                case 5:
                    state = 6;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
