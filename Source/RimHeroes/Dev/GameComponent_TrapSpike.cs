using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Trap logic check: -quicktest -rhtrap. Drives Building_DungeonTrap with forced rolls (no UI) to
    /// confirm the Perception spot reveals a hidden trap (and a miss leaves it hidden), the Dodge save
    /// dodges-vs-takes-damage, the disarm neutralizes safely or springs on a fumble, and that every
    /// outcome spends the one-shot trap. Logs each assertion and a verdict, then exits.
    /// </summary>
    public class GameComponent_TrapSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhtrap");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_TrapSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.Trap] === spot-then-save logic ===");

            // 1) Spot: a passing nat-20 reveals a hidden trap; a nat-1 leaves it hidden.
            var spotHit = MakeTrap(map, new IntVec3(map.Center.x + 5, 0, map.Center.z));
            var a = MakePawn(map);
            Assert(spotHit.IsHidden, "trap starts hidden");
            spotHit.TrySpot(a, 20);
            Assert(!spotHit.IsHidden, "a successful Perception spot reveals the trap");
            var spotMiss = MakeTrap(map, new IntVec3(map.Center.x - 5, 0, map.Center.z));
            spotMiss.TrySpot(MakePawn(map), 1);
            Assert(spotMiss.IsHidden, "a failed spot leaves the trap hidden");

            // 2) Save: a passed Dodge dodges unharmed; a failed Dodge takes damage. One-shot either way.
            var saveDodge = MakeTrap(map, new IntVec3(map.Center.x + 5, 0, map.Center.z + 5));
            var dodger = MakePawn(map);
            float dodgerBefore = dodger.health.summaryHealth.SummaryHealthPercent;
            saveDodge.ResolveSave(dodger, ForcedRoll(20));
            Assert(dodger.health.summaryHealth.SummaryHealthPercent >= dodgerBefore && saveDodge.Destroyed, "a passed save dodges unharmed and spends the trap");

            var saveHit = MakeTrap(map, new IntVec3(map.Center.x - 5, 0, map.Center.z + 5));
            var victim = MakePawn(map);
            float victimBefore = victim.health.summaryHealth.SummaryHealthPercent;
            saveHit.ResolveSave(victim, ForcedRoll(2));
            Assert(victim.health.summaryHealth.SummaryHealthPercent < victimBefore && saveHit.Destroyed, "a failed save takes damage and spends the trap");

            // 3) Disarm: a pass neutralizes safely; a fumble springs on the disarmer.
            var disSafe = MakeTrap(map, new IntVec3(map.Center.x + 5, 0, map.Center.z - 5));
            var tinker = MakePawn(map);
            float tinkerBefore = tinker.health.summaryHealth.SummaryHealthPercent;
            disSafe.ResolveDisarm(tinker, ForcedRoll(20));
            Assert(tinker.health.summaryHealth.SummaryHealthPercent >= tinkerBefore && disSafe.Destroyed, "a passed disarm neutralizes safely");

            var disFumble = MakeTrap(map, new IntVec3(map.Center.x - 5, 0, map.Center.z - 5));
            var fumbler = MakePawn(map);
            float fumblerBefore = fumbler.health.summaryHealth.SummaryHealthPercent;
            disFumble.ResolveDisarm(fumbler, ForcedRoll(1));
            Assert(fumbler.health.summaryHealth.SummaryHealthPercent < fumblerBefore && disFumble.Destroyed, "a fumbled disarm springs on the disarmer");

            Log.Message($"[RimHeroes.Trap] RESULT: trap logic verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private static Building_DungeonTrap MakeTrap(Map map, IntVec3 pos)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Trap_Blade");
            var t = (Building_DungeonTrap)ThingMaker.MakeThing(def);
            GenSpawn.Spawn(t, pos, map);
            return t;
        }

        private static Pawn MakePawn(Map map)
        {
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true));
            GenSpawn.Spawn(p, map.Center, map);
            return p;
        }

        private static RollResult ForcedRoll(int raw) => D20.Roll(0, 13, RollAdvantage.None, raw);

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.Trap] OK: {what}");
            else { Log.Warning($"[RimHeroes.Trap] FAIL: {what}"); ok = false; }
        }
    }
}
