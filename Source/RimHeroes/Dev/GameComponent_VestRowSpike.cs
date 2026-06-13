using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Vestment review: launch with -quicktest -rhvestrowspike [-rhvestclass=RH_Barbarian].
    /// Spawns five male-bodied heroes of one class at levels 1/5/10/15/20 in a row,
    /// screenshots south, east, and north, then exits.
    /// </summary>
    public class GameComponent_VestRowSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhvestrowspike");

        private int state;
        private float nextStateTime = -1f;
        private Pawn[] pawns;
        private IntVec3 anchor;

        private static readonly int[] Levels = { 1, 5, 10, 15, 20 };

        public GameComponent_VestRowSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 6)
            {
                return;
            }
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f)
            {
                nextStateTime = now + 4f;
                return;
            }
            if (now < nextStateTime)
            {
                return;
            }
            nextStateTime = now + 3f;

            switch (state)
            {
                case 0:
                {
                    string className = "RH_Barbarian";
                    GenCommandLine.TryGetCommandLineArg("rhvestclass", out var cls);
                    if (!cls.NullOrEmpty()) className = cls;
                    var classDef = DefDatabase<HeroClassDef>.GetNamed(className);
                    // anchor away from the quicktest base so loot/walls don't photobomb the row
                    var searchFrom = map.Center + new IntVec3(0, 0, -35);
                    CellFinder.TryFindRandomCellNear(searchFrom, map, 10, c => c.Standable(map), out anchor);
                    pawns = new Pawn[Levels.Length];
                    for (int i = 0; i < Levels.Length; i++)
                    {
                        try
                        {
                            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                                fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true,
                                fixedGender: Gender.Male));
                            pawns[i] = p;
                            GenSpawn.Spawn(p, anchor + new IntVec3(i * 3 - 6, 0, 0), map);
                            p.apparel?.DestroyAll();
                            if (p.story != null)
                            {
                                p.story.bodyType = BodyTypeDefOf.Male;
                            }
                            var levels = HeroUtility.MakeHero(p, classDef);
                            levels.SetLevelDirect(Levels[i]);
                            p.Drawer?.renderer?.SetAllGraphicsDirty();
                            if (p.drafter != null)
                            {
                                p.drafter.Drafted = true;
                            }
                            Log.Message($"[RimHeroes.VestRowSpike] pawn {i} level {Levels[i]} ready");
                        }
                        catch (System.Exception e)
                        {
                            Log.Error($"[RimHeroes.VestRowSpike] pawn {i} failed: {e}");
                        }
                    }
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 9f);
                    state = 1;
                    break;
                }
                // pin and shoot in separate states: the rotation set in GameComponentUpdate only
                // reaches the renderer on the NEXT frame, so a same-frame screenshot captures stale facing
                case 1:
                    Find.TickManager.Pause();
                    Pin(Rot4.South);
                    state = 2;
                    break;
                case 2:
                    ScreenshotTaker.TakeNonSteamShot("rhvestrow_south");
                    Pin(Rot4.East);
                    state = 3;
                    break;
                case 3:
                    ScreenshotTaker.TakeNonSteamShot("rhvestrow_east");
                    Pin(Rot4.North);
                    state = 4;
                    break;
                case 4:
                    ScreenshotTaker.TakeNonSteamShot("rhvestrow_north");
                    state = 5;
                    break;
                case 5:
                    Log.Message("[RimHeroes.VestRowSpike] RESULT: vestment row shots taken verdict=PASS");
                    state = 6;
                    Root.Shutdown();
                    break;
            }
        }

        private void Pin(Rot4 rot)
        {
            // -quicktest forces devMode on, so map-load errors from other mods auto-open the
            // debug log window and it photobombs the row. Close any log/edit window before each shot.
            var ws = Find.WindowStack;
            if (ws != null)
            {
                foreach (var w in ws.Windows.ToList())
                {
                    var n = w.GetType().Name;
                    if (n.Contains("Log") || n.Contains("EditWindow")) w.Close(false);
                }
            }
            // re-assert the camera every shot - quicktest occasionally resets zoom after load
            Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 9f);
            foreach (var p in pawns)
            {
                if (p == null) continue;
                p.jobs?.StopAll();
                p.Rotation = rot;
            }
            for (int i = 0; i < pawns.Length; i++)
            {
                if (pawns[i] == null) continue;
                var sp = Find.Camera.WorldToScreenPoint(pawns[i].DrawPos);
                Log.Message($"[RimHeroes.VestRowSpike] SCREENPOS {rot.ToStringHuman()} {i} {sp.x:F0} {Screen.height - sp.y:F0}");
            }
        }
    }
}
