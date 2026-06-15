using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Crypt generation review: -quicktest -rhdungeongen. Generates the crypt pocket map directly,
    /// switches the view to it, frames the whole map, and screenshots so the room/corridor layout can
    /// be eyeballed. Output: Screenshots/rhdungeon.png.
    /// </summary>
    public class GameComponent_DungeonGenSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhdungeongen");

        private int state;
        private float nextTime = -1f;
        private Map dungeon;

        public GameComponent_DungeonGenSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 2) return;
            var src = Find.CurrentMap;
            if (src == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextTime < 0f) { nextTime = now + 4f; return; }
            if (now < nextTime) return;

            switch (state)
            {
                case 0:
                {
                    var gen = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("RH_DungeonGen");
                    if (gen == null) { Log.Error("[RimHeroes.DungeonGen] missing RH_DungeonGen"); state = 3; return; }
                    try
                    {
                        dungeon = PocketMapUtility.GeneratePocketMap(new IntVec3(64, 1, 64), gen, null, src);
                        Current.Game.CurrentMap = dungeon;
                        var comp = dungeon.GetComponent<MapComponent_CryptDungeon>();
                        Log.Message($"[RimHeroes.DungeonGen] generated {dungeon.Size.x}x{dungeon.Size.z}, rooms={comp?.rooms?.Count ?? -1}");
                    }
                    catch (System.Exception e) { Log.Error($"[RimHeroes.DungeonGen] gen failed: {e}"); state = 3; return; }
                    Find.CameraDriver.SetRootPosAndSize(dungeon.Center.ToVector3(), 36f);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    nextTime = now + 3f;
                    state = 1;
                    break;
                }
                case 1:
                    foreach (var w in Find.WindowStack.Windows.ToList())
                        if (!(w is MainTabWindow)) w.Close(false);
                    Messages.Clear();
                    Find.CameraDriver.SetRootPosAndSize(dungeon.Center.ToVector3(), 36f);
                    ScreenshotTaker.TakeNonSteamShot("rhdungeon");
                    nextTime = now + 1.5f;
                    state = 2;
                    break;
                case 2:
                    Log.Message("[RimHeroes.DungeonGen] RESULT: crypt gen shot taken verdict=PASS");
                    state = 3;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
