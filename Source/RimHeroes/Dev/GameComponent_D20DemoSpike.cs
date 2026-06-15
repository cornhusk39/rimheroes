using System;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// d20 dialog review: -quicktest -rhd20demo. Opens the roll dialog four times with forced results
    /// (nat 20, a plain success, a plain failure, nat 1) and screenshots each landed state, so the
    /// outcome presentation can be eyeballed. Output: Screenshots/rhd20_{crit,success,fail,critfail}.png.
    /// </summary>
    public class GameComponent_D20DemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhd20demo");

        private static readonly int[] Raws = { 20, 15, 6, 1 };
        private static readonly string[] Names = { "crit", "success", "fail", "critfail" };
        private const int ForcedMod = 3;

        private int idx;
        private int phase;          // 0 = open, 1 = screenshot+advance, 2 = finishing
        private float nextTime = -1f;
        private Pawn pawn;

        public GameComponent_D20DemoSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || phase > 2) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextTime < 0f) { nextTime = now + 4f; return; }
            if (now < nextTime) return;

            if (pawn == null)
            {
                pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault()
                       ?? PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            }
            var check = DefDatabase<CheckDef>.GetNamedSilentFail("RH_Check_LockPick");

            switch (phase)
            {
                case 0:
                    CloseWindows(null);   // clear everything (incl. the previous roll + auto class dialogs)
                    if (check != null)
                    {
                        var result = Make(Raws[idx], ForcedMod, check.dc);
                        Find.WindowStack.Add(new Dialog_D20Roll(pawn, check, result, null));
                    }
                    nextTime = now + 2.9f;   // past the 1.5s spin + landing + a beat
                    phase = 1;
                    break;
                case 1:
                    CloseWindows(typeof(Dialog_D20Roll));   // close photobombers, keep the roll dialog
                    ScreenshotTaker.TakeNonSteamShot("rhd20_" + Names[idx]);
                    idx++;
                    if (idx >= Raws.Length) { nextTime = now + 1.5f; phase = 2; }
                    else { nextTime = now + 0.5f; phase = 0; }
                    break;
                case 2:
                    Log.Message("[RimHeroes.D20Demo] RESULT: d20 demo done verdict=PASS");
                    phase = 3;
                    Root.Shutdown();
                    break;
            }
        }

        private static RollResult Make(int raw, int mod, int dc)
        {
            int total = raw + mod;
            RollBucket bucket;
            if (raw == 20) bucket = RollBucket.CriticalSuccess;
            else if (raw == 1) bucket = RollBucket.CriticalFailure;
            else if (total >= dc) bucket = RollBucket.Success;
            else bucket = RollBucket.Failure;
            return new RollResult { rawDie = raw, modifier = mod, total = total, dc = dc, bucket = bucket };
        }

        private static void CloseWindows(Type keep)
        {
            foreach (var w in Find.WindowStack.Windows.ToList())
            {
                if (w is MainTabWindow) continue;
                if (keep != null && w.GetType() == keep) continue;
                w.Close(false);
            }
        }
    }
}
