using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Crypt Lord review: -quicktest -rhcryptlord. Spawns the boss on a cleared OUTDOOR pad in full
    /// daylight, lets the vestment render, frames it close, and screenshots. Output: rhcryptlord.png.
    /// </summary>
    public class GameComponent_CryptLordSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhcryptlord");

        private int state;
        private float nextTime = -1f;
        private Pawn boss;

        public GameComponent_CryptLordSpike(Game game) { }

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
                    CellFinder.TryFindRandomCellNear(map.Center, map, 12,
                        c => c.Standable(map) && !c.Roofed(map) && c.GetEdifice(map) == null, out var cell);
                    foreach (var c in CellRect.CenteredOn(cell, 5, 5).Cells.Where(c => c.InBounds(map)))
                    {
                        foreach (var t in c.GetThingList(map).Where(t => !(t is Pawn) && t.def.destroyable).ToList()) t.Destroy(DestroyMode.Vanish);
                        map.roofGrid.SetRoof(c, null);
                        map.snowGrid?.SetDepth(c, 0f);
                    }
                    boss = DungeonBoss.SpawnCryptLord(map, cell, Faction.OfEntities);
                    Find.CameraDriver.SetRootPosAndSize((boss?.Position ?? cell).ToVector3(), 6f);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;   // tick so the vestment mesh builds
                    nextTime = now + 3f;
                    state = 1;
                    break;
                }
                case 1:
                    foreach (var w in Find.WindowStack.Windows.ToList())
                        if (!(w is MainTabWindow)) w.Close(false);
                    Messages.Clear();
                    if (boss != null)
                    {
                        boss.jobs?.StopAll();
                        boss.Rotation = Rot4.South;
                        Find.CameraDriver.SetRootPosAndSize(boss.Position.ToVector3(), 6f);
                    }
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    nextTime = now + 1.2f;
                    state = 2;
                    break;
                case 2:
                    foreach (var w in Find.WindowStack.Windows.ToList())
                        if (!(w is MainTabWindow)) w.Close(false);
                    if (boss != null) { boss.jobs?.StopAll(); boss.Rotation = Rot4.South; }
                    ScreenshotTaker.TakeNonSteamShot("rhcryptlord");
                    Log.Message("[RimHeroes.CryptLord] RESULT: boss shot taken verdict=PASS");
                    state = 3;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
