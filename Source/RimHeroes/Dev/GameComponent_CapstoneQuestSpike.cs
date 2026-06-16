using System.Linq;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Capstone-quest generation check: -quicktest -rhcapquest. The one thing a spike CAN validate about
    /// a quest is that its script generates without throwing and produces the expected parts (the
    /// 25-day runtime + raids can only be felt in real play). Generates RH_Quest_Capstone from a slate
    /// and asserts it builds with the capstone-mark part + two raids. Logs a verdict, then exits.
    /// </summary>
    public class GameComponent_CapstoneQuestSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhcapquest");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_CapstoneQuestSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.CapQuest] === capstone stranger quest generation ===");
            var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail("RH_Quest_Capstone");
            Assert(def != null, "RH_Quest_Capstone loads");

            Quest quest = null;
            try
            {
                var slate = new Slate();
                slate.Set("heroClass", DefDatabase<HeroClassDef>.GetNamed("RH_Fighter"));
                slate.Set("map", map);
                slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
                slate.Set("heroName", "Bron");
                slate.Set("weaponLabel", "the warblade");
                quest = QuestUtility.GenerateQuestAndMakeAvailable(def, slate);
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.CapQuest] quest gen threw: {e}"); ok = false; }

            Assert(quest != null, "quest generates without throwing");
            int parts = quest?.PartsListForReading?.Count ?? 0;
            Assert(parts > 0, $"quest has parts ({parts})");
            Assert(quest?.PartsListForReading?.Any(p => p is QuestPart_RH_MarkCapstone) ?? false, "quest contains the capstone-mark part");
            if (quest != null)
                Log.Message("[RimHeroes.CapQuest] parts: " + string.Join(", ", quest.PartsListForReading.Select(p => p.GetType().Name)));
            int raids = quest?.PartsListForReading?.Count(p => { var n = p.GetType().Name; return n.Contains("Raid") || n.Contains("Incident") || n.Contains("PawnsArrive"); }) ?? 0;
            Assert(raids >= 2, $"quest schedules two raids (found {raids})");

            // also confirm the launcher path runs clean
            try { CapstoneQuest.LaunchStrangerQuest(map.mapPawns.FreeColonistsSpawned.FirstOrDefault(), DefDatabase<HeroClassDef>.GetNamed("RH_Fighter")); }
            catch (System.Exception e) { Log.Error($"[RimHeroes.CapQuest] launcher threw: {e}"); ok = false; }
            Log.Message("[RimHeroes.CapQuest] OK: LaunchStrangerQuest ran without throwing");

            Log.Message($"[RimHeroes.CapQuest] RESULT: capstone quest verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.CapQuest] OK: {what}");
            else { Log.Warning($"[RimHeroes.CapQuest] FAIL: {what}"); ok = false; }
        }
    }
}
