using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Art reference capture: launch with -quicktest -rhrefspike. Spawns vanilla bear, wolf, and
    /// deer beside our beasts, pins rotations, zooms, screenshots east + south rows, exits.
    /// </summary>
    public class GameComponent_RefSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhrefspike");

        private int state;
        private float nextStateTime = -1f;
        private Pawn[] animals;
        private IntVec3 anchor;

        private static readonly string[] Kinds =
            { "Bear_Grizzly", "Wolf_Timber", "Deer" };

        public GameComponent_RefSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 5)
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
            if (Find.TickManager.Paused)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            }

            switch (state)
            {
                case 0:
                {
                    anchor = map.Center;
                    CellFinder.TryFindRandomCellNear(map.Center, map, 15, c => c.Standable(map), out anchor);
                    animals = new Pawn[Kinds.Length];
                    for (int i = 0; i < Kinds.Length; i++)
                    {
                        var kind = PawnKindDef.Named(Kinds[i]);
                        animals[i] = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer, fixedBiologicalAge: 10f, fixedChronologicalAge: 10f));
                        GenSpawn.Spawn(animals[i], anchor + new IntVec3((i % 3) * 5 - 5, 0, (i / 3) * -5 + 2), map);
                    }
                    state = 1;
                    break;
                }
                case 1:
                    Pin(Rot4.East);
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 13f);
                    state = 2;
                    break;
                case 2:
                    ScreenshotTaker.TakeNonSteamShot("rhref_east");
                    state = 3;
                    break;
                case 3:
                    Pin(Rot4.South);
                    state = 4;
                    break;
                case 4:
                    ScreenshotTaker.TakeNonSteamShot("rhref_south");
                    Log.Message("[RimHeroes.RefSpike] RESULT: reference shots taken verdict=PASS");
                    state = 5;
                    break;
                case 5:
                    state = 6;
                    Root.Shutdown();
                    break;
            }
        }

        private void Pin(Rot4 rot)
        {
            foreach (var a in animals)
            {
                a.jobs?.StopAll();
                a.Rotation = rot;
            }
        }
    }
}
