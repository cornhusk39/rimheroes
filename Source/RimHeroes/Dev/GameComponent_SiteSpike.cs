using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// World-tile dungeon site check: -quicktest -rhsite. Fires the dungeon-quest incident and confirms
    /// it drops a Site carrying a DungeonKind, then drives the site-map populate directly to confirm it
    /// places the entrance + a guard force. Logs assertions + a verdict, then exits.
    /// </summary>
    public class GameComponent_SiteSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhsite");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_SiteSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.Site] === world-tile dungeon site ===");

            // 1) the incident drops a site with a dungeon kind on it
            var inc = DefDatabase<IncidentDef>.GetNamedSilentFail("RH_Incident_DungeonQuest");
            bool fired = inc != null && inc.Worker.TryExecuteWorker(new IncidentParms { target = map });
            var site = Find.WorldObjects.AllWorldObjects.OfType<Site>()
                .FirstOrDefault(s => s.GetComponent<WorldObjectComp_DungeonSite>()?.kind != null);
            Assert(fired && site != null, "the dungeon-quest incident drops a site");
            Assert(site?.GetComponent<WorldObjectComp_DungeonSite>()?.kind != null, "the site carries a dungeon kind");

            // 2) the site populate places the entrance + a guard force
            var crypt = DefDatabase<DungeonKindDef>.GetNamedSilentFail("RH_Dungeon_Crypt");
            int hostilesBefore = Hostiles(map);
            GenStep_DungeonSiteGuards.Populate(map, crypt, 3, 2f);
            bool entrance = map.listerThings.AllThings.Any(t => t.def.defName == "RH_DungeonEntrance");
            Assert(entrance, "the site populate places a dungeon entrance");
            Assert(Hostiles(map) > hostilesBefore, $"the site populate spawns a guard force ({hostilesBefore} -> {Hostiles(map)})");

            Log.Message($"[RimHeroes.Site] RESULT: world-tile site verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private static int Hostiles(Map map) => map.mapPawns.AllPawnsSpawned.Count(p => !p.Dead && p.HostileTo(Faction.OfPlayer));

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.Site] OK: {what}");
            else { Log.Warning($"[RimHeroes.Site] FAIL: {what}"); ok = false; }
        }
    }
}
