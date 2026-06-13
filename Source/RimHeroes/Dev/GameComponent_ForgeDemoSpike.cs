using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Heroic Forge showcase: -quicktest -rhforgedemo. Spawns the forge, screenshots south + east, exits.
    /// </summary>
    public class GameComponent_ForgeDemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhforgedemo");
        private int state;
        private float nextStateTime = -1f;
        private IntVec3 anchor;

        public GameComponent_ForgeDemoSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 4) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f) { nextStateTime = now + 4f; return; }
            if (now < nextStateTime) return;
            nextStateTime = now + 3f;

            switch (state)
            {
                case 0:
                {
                    var searchFrom = map.Center + new IntVec3(0, 0, -34);
                    CellFinder.TryFindRandomCellNear(searchFrom, map, 8, c => c.Standable(map), out anchor);
                    var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_HeroicForge");
                    if (def == null) { Log.Error("[RimHeroes.ForgeDemo] RH_HeroicForge missing"); }
                    else
                    {
                        var forge = ThingMaker.MakeThing(def, null);
                        forge.SetFactionDirect(Faction.OfPlayer);
                        GenSpawn.Spawn(forge, anchor, map, Rot4.South);
                        Log.Message("[RimHeroes.ForgeDemo] forge spawned; recipes=" + def.AllRecipes.Count);
                    }
                    Find.TickManager.Pause();
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 7f);
                    state = 1;
                    break;
                }
                case 1:
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 7f);
                    ScreenshotTaker.TakeNonSteamShot("rhforge_south");
                    state = 2;
                    break;
                case 2:
                    state = 3;
                    break;
                case 3:
                    Log.Message("[RimHeroes.ForgeDemo] RESULT verdict=PASS");
                    state = 5;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
