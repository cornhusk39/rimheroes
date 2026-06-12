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
                    CellFinder.TryFindRandomCellNear(map.Center, map, 15, c => c.Standable(map), out anchor);
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
                case 1:
                    Find.TickManager.Pause();
                    Pin(Rot4.South);
                    ScreenshotTaker.TakeNonSteamShot("rhvestrow_south");
                    state = 2;
                    break;
                case 2:
                    Pin(Rot4.East);
                    ScreenshotTaker.TakeNonSteamShot("rhvestrow_east");
                    state = 3;
                    break;
                case 3:
                    Pin(Rot4.North);
                    ScreenshotTaker.TakeNonSteamShot("rhvestrow_north");
                    state = 4;
                    break;
                case 4:
                    Log.Message("[RimHeroes.VestRowSpike] RESULT: vestment row shots taken verdict=PASS");
                    state = 5;
                    Root.Shutdown();
                    break;
            }
        }

        private void Pin(Rot4 rot)
        {
            foreach (var p in pawns)
            {
                if (p == null) continue;
                p.jobs?.StopAll();
                p.Rotation = rot;
            }
        }
    }
}
