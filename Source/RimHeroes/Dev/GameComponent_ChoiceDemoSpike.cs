using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Level-up choice verification: -quicktest -rhchoicedemo. Confirms the interactive trait/feat
    /// dialogs fire for a PLAYER hero (and that a non-player hero auto-picks silently instead).
    ///
    /// Player wizard: GainXP across L3/L4 -> expect a Dialog_HeroChoice open on the WindowStack and
    ///   the L3 choice still UNRESOLVED (waiting on the player).
    /// Enemy wizard: same XP -> expect NO dialog and the L3/L4 choices already auto-resolved.
    /// Then closes the dialog and exits.
    /// </summary>
    public class GameComponent_ChoiceDemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhchoicedemo");
        private int state;
        private float nextStateTime = -1f;

        public GameComponent_ChoiceDemoSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 1) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f) { nextStateTime = now + 4f; return; }
            if (now < nextStateTime) return;
            try { RunDemo(map); }
            catch (System.Exception e) { Log.Error("[RimHeroes.ChoiceDemo] " + e); }
            state = 2;
            Root.Shutdown();
        }

        private void RunDemo(Map map)
        {
            // ---- Player wizard: should open an interactive dialog and leave the choice unresolved ----
            CellFinder.TryFindRandomCellNear(map.Center, map, 10, c => c.Standable(map), out var anchor);
            var playerHero = SpawnWizard(map, anchor, Faction.OfPlayer);
            bool playerControlled = playerHero.pawn.IsColonistPlayerControlled;
            int dialogsBefore = OpenChoiceDialogs();
            playerHero.GainXP(400f); // crosses L3 (trait pick) and L4 (feat pick)
            int dialogsAfter = OpenChoiceDialogs();
            bool l3Unresolved = !playerHero.IsChoiceResolved(3);
            Log.Message($"[RimHeroes.ChoiceDemo] PLAYER wizard: level={playerHero.level} isColonistPlayerControlled={playerControlled} " +
                        $"choiceDialogsOpen={dialogsAfter} (was {dialogsBefore}) L3resolved={!l3Unresolved}");
            bool playerPass = playerControlled && dialogsAfter > dialogsBefore && l3Unresolved;

            // Close any open choice dialog so the screenshot/exit is clean.
            foreach (var w in Find.WindowStack.Windows.OfType<Dialog_HeroChoice>().ToList())
            {
                w.Close(false);
            }

            // ---- Enemy wizard: should auto-pick with NO dialog ----
            CellFinder.TryFindRandomCellNear(map.Center, map, 14, c => c.Standable(map), out var anchor2);
            var enemyHero = SpawnWizard(map, anchor2, Faction.OfPirates);
            int dialogsBeforeEnemy = OpenChoiceDialogs();
            enemyHero.GainXP(400f);
            int dialogsAfterEnemy = OpenChoiceDialogs();
            bool enemyResolved = enemyHero.IsChoiceResolved(3) && enemyHero.IsChoiceResolved(4);
            Log.Message($"[RimHeroes.ChoiceDemo] ENEMY wizard: level={enemyHero.level} isColonistPlayerControlled={enemyHero.pawn.IsColonistPlayerControlled} " +
                        $"choiceDialogsOpen={dialogsAfterEnemy} (was {dialogsBeforeEnemy}) L3+L4autoResolved={enemyResolved} " +
                        $"feats={enemyHero.TakenFeats.Count}");
            bool enemyPass = dialogsAfterEnemy == dialogsBeforeEnemy && enemyResolved;

            Log.Message($"[RimHeroes.ChoiceDemo] RESULT: playerDialogFires={playerPass} enemyAutoPicks={enemyPass} " +
                        $"verdict={(playerPass && enemyPass ? "PASS" : "FAIL")}");
        }

        private static int OpenChoiceDialogs() => Find.WindowStack.Windows.OfType<Dialog_HeroChoice>().Count();

        private Hediff_HeroLevels SpawnWizard(Map map, IntVec3 cell, Faction faction)
        {
            var cd = DefDatabase<HeroClassDef>.GetNamed("RH_Wizard");
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                faction, forceGenerateNewPawn: true, fixedGender: Gender.Male));
            GenSpawn.Spawn(p, cell, map);
            return HeroUtility.MakeHero(p, cd);
        }
    }
}
