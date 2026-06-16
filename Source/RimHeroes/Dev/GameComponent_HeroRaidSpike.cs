using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Hero-party raid check: -quicktest -rhheroraid. Fires RH_Incident_HeroRaid and confirms it spawns
    /// a band of hostile enemy HEROES (our classes) scaled to points. Logs a verdict, then exits.
    /// </summary>
    public class GameComponent_HeroRaidSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhheroraid");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_HeroRaidSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.HeroRaid] === enemy hero-party raid ===");
            var inc = DefDatabase<IncidentDef>.GetNamedSilentFail("RH_Incident_HeroRaid");
            Assert(inc != null, "RH_Incident_HeroRaid loads");

            int before = EnemyHeroes(map);
            bool fired = false;
            try { fired = inc.Worker.TryExecuteWorker(new IncidentParms { target = map, points = 2500f }); }
            catch (System.Exception e) { Log.Error($"[RimHeroes.HeroRaid] threw: {e}"); ok = false; }
            Assert(fired, "the raid fires");
            int after = EnemyHeroes(map);
            Assert(after - before >= 2, $"it spawns a party of enemy heroes ({before} -> {after})");
            var sample = map.mapPawns.AllPawnsSpawned.Where(p => p.HostileTo(Faction.OfPlayer) && HeroUtility.IsHero(p)).Take(6)
                .Select(p => HeroUtility.GetHeroHediff(p)?.classDef?.label).Where(l => l != null);
            Log.Message("[RimHeroes.HeroRaid] party classes: " + string.Join(", ", sample));

            Log.Message($"[RimHeroes.HeroRaid] RESULT: hero raid verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private static int EnemyHeroes(Map map) =>
            map.mapPawns.AllPawnsSpawned.Count(p => !p.Dead && p.HostileTo(Faction.OfPlayer) && HeroUtility.IsHero(p));

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.HeroRaid] OK: {what}");
            else { Log.Warning($"[RimHeroes.HeroRaid] FAIL: {what}"); ok = false; }
        }
    }
}
