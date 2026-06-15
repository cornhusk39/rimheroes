using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Reliquary lock logic check: -quicktest -rhreliquary. Drives Building_Reliquary.ResolveAttempt
    /// with forced rolls (no UI) to confirm the DC escalation, the seal-at-25 jam, a normal success
    /// spilling its inlays, and a natural-20 popping a high-DC lock. Logs each assertion and a final
    /// verdict, then exits.
    /// </summary>
    public class GameComponent_ReliquarySpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhreliquary");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_ReliquarySpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.Reliquary] === lock logic ===");
            var picker = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true));
            GenSpawn.Spawn(picker, map.Center, map);

            // 1) Tier-3 lock (DC 15): two failures should climb 15 -> 20 -> jam at 25.
            var jam = MakeReliquary(map, new IntVec3(map.Center.x + 4, 0, map.Center.z), tier: 3);
            Assert(jam.CurrentDC == 15, $"tier-3 starts DC 15 (got {jam.CurrentDC})");
            jam.ResolveAttempt(picker, ForcedRoll(2, jam.CurrentDC));   // fail
            Assert(jam.CurrentDC == 20 && jam.CanAttempt, $"after 1 fail DC 20 + still pickable (DC {jam.CurrentDC}, can {jam.CanAttempt})");
            jam.ResolveAttempt(picker, ForcedRoll(2, jam.CurrentDC));   // fail -> would be 25
            Assert(jam.CurrentDC >= 25 && !jam.CanAttempt, $"second fail jams the lock (DC {jam.CurrentDC}, can {jam.CanAttempt})");

            // 2) Tier-1 lock (DC 5): a plain success opens it and spills at least one inlay.
            var win = MakeReliquary(map, new IntVec3(map.Center.x - 4, 0, map.Center.z), tier: 1);
            Assert(win.CurrentDC == 5, $"tier-1 starts DC 5 (got {win.CurrentDC})");
            win.ResolveAttempt(picker, ForcedRoll(10, win.CurrentDC));  // 10 >= 5 -> success
            Assert(!win.CanAttempt, "opened lock is no longer pickable");
            Assert(InlaysNear(map, win.Position) >= 1, $"success spilled an inlay (found {InlaysNear(map, win.Position)})");

            // 3) Natural 20 pops even a near-sealed lock (raw 20 = crit success regardless of DC).
            var crit = MakeReliquary(map, new IntVec3(map.Center.x, 0, map.Center.z + 4), tier: 3);
            crit.ResolveAttempt(picker, ForcedRoll(2, crit.CurrentDC));  // 15 -> 20
            crit.ResolveAttempt(picker, ForcedRoll(20, crit.CurrentDC)); // nat 20 at DC 20
            Assert(!crit.CanAttempt && InlaysNear(map, crit.Position) >= 1, "natural 20 opens a hard lock");

            // 4) A carried reliquary key auto-opens even a hard lock and is consumed.
            var keyTest = MakeReliquary(map, new IntVec3(map.Center.x + 4, 0, map.Center.z + 4), tier: 3);
            GiveItem(picker, Building_Reliquary.KeyDef, 1);
            bool hadKey = Building_Reliquary.CountItem(picker, Building_Reliquary.KeyDef) == 1;
            bool keyOpened = keyTest.TryOpenWithKey(picker);
            Assert(hadKey && keyOpened && !keyTest.CanAttempt
                   && Building_Reliquary.CountItem(picker, Building_Reliquary.KeyDef) == 0
                   && InlaysNear(map, keyTest.Position) >= 1, "reliquary key auto-opens a hard lock and is consumed");

            // 5) A lockpick cuts the DC by 7, is spent, and turns a failing roll into a success.
            var pickTest = MakeReliquary(map, new IntVec3(map.Center.x - 4, 0, map.Center.z + 4), tier: 3); // DC 15
            GiveItem(picker, Building_Reliquary.LockpickDef, 2);
            int baseDC = pickTest.CurrentDC;
            int redDC = Building_Reliquary.ReducedDC(baseDC);
            bool spent = Building_Reliquary.ConsumeOne(picker, Building_Reliquary.LockpickDef);
            Assert(redDC == baseDC - 7 && spent && Building_Reliquary.CountItem(picker, Building_Reliquary.LockpickDef) == 1,
                   $"lockpick cuts DC {baseDC}->{redDC} and spends one (1 left)");
            Assert(!ForcedRoll(baseDC - 3, baseDC).Passed && ForcedRoll(baseDC - 3, redDC).Passed,
                   "the reduced DC turns a failing roll into a success");

            Log.Message($"[RimHeroes.Reliquary] RESULT: reliquary lock verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private static void GiveItem(Pawn p, string defName, int count)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null) return;
            var t = ThingMaker.MakeThing(def);
            t.stackCount = count;
            p.inventory.innerContainer.TryAdd(t);
        }

        private static Building_Reliquary MakeReliquary(Map map, IntVec3 pos, int tier)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Reliquary");
            var b = (Building_Reliquary)ThingMaker.MakeThing(def);
            b.lockTier = tier;
            GenSpawn.Spawn(b, pos, map);
            return b;
        }

        private static RollResult ForcedRoll(int raw, int dc) => D20.Roll(0, dc, RollAdvantage.None, raw);

        private static int InlaysNear(Map map, IntVec3 pos)
        {
            return GenRadial.RadialCellsAround(pos, 6f, true)
                .Where(c => c.InBounds(map))
                .SelectMany(c => c.GetThingList(map))
                .Count(t => t.def.defName.StartsWith("RH_InlayItem_"));
        }

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.Reliquary] OK: {what}");
            else { Log.Warning($"[RimHeroes.Reliquary] FAIL: {what}"); ok = false; }
        }
    }
}
