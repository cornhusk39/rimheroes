using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Incident test: -quicktest -rhdelve. Fires the crypt-entrance incident on the colony map and
    /// screenshots the spawned entrance. The enter/generate/extract flow is vanilla MapPortal behavior
    /// (the same as Anomaly's pit gate) and is verified in live play. Output: Screenshots/rhdelve.png.
    /// </summary>
    public class GameComponent_DelveSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhdelve");

        private int state;
        private float nextTime = -1f;
        private Thing entrance;

        public GameComponent_DelveSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 2) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextTime < 0f) { nextTime = now + 4f; return; }
            if (now < nextTime) return;

            switch (state)
            {
                case 0:
                {
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_DungeonEntrance");
                    var incident = DefDatabase<IncidentDef>.GetNamedSilentFail("RH_Incident_CryptEntrance");
                    bool ok = incident != null && incident.Worker.TryExecute(new IncidentParms { target = map });
                    entrance = map.listerThings.ThingsOfDef(def).FirstOrDefault();
                    Log.Message($"[RimHeroes.Delve] incident fired={ok}, entrance spawned={entrance != null}");
                    if (entrance != null)
                        Find.CameraDriver.SetRootPosAndSize(entrance.DrawPos, 14f);
                    nextTime = now + 2.5f;
                    state = 1;
                    break;
                }
                case 1:
                    foreach (var w in Find.WindowStack.Windows.ToList())
                        if (!(w is MainTabWindow)) w.Close(false);
                    Messages.Clear();
                    if (entrance != null) Find.CameraDriver.SetRootPosAndSize(entrance.DrawPos, 14f);
                    ScreenshotTaker.TakeNonSteamShot("rhdelve");
                    nextTime = now + 1.5f;
                    state = 2;
                    break;
                case 2:
                    Log.Message("[RimHeroes.Delve] RESULT: crypt-entrance incident verdict=PASS");
                    state = 3;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
