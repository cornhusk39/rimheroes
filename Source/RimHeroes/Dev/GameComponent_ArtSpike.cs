using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Art proofing: launch with -quicktest -rhartspike. Spawns three Porter mims locked to
    /// south/east/north rotations beside a drafted L5 Wizard (Fireball gizmo icon visible),
    /// zooms in, screenshots, exits.
    /// </summary>
    public class GameComponent_ArtSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhartspike");

        private int state;
        private float nextStateTime = -1f;
        private Pawn hero;
        private Pawn[] porters;

        public GameComponent_ArtSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 3)
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
                    hero = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                    if (hero == null)
                    {
                        hero = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(hero, map.Center, map);
                    }
                    HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Wizard")).SetLevelDirect(5);
                    foreach (var psycast in new[] { "Painblock", "Skip", "Wallraise", "Burden" })
                    {
                        var def = DefDatabase<AbilityDef>.GetNamedSilentFail(psycast);
                        if (def != null)
                        {
                            hero.abilities.GainAbility(def);
                        }
                    }
                    hero.drafter.Drafted = true;
                    var kind = PawnKindDef.Named("RH_MimPorterKind");
                    var rots = new[] { Rot4.South, Rot4.East, Rot4.North };
                    porters = new Pawn[3];
                    for (int i = 0; i < 3; i++)
                    {
                        porters[i] = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer));
                        GenSpawn.Spawn(porters[i], hero.Position + new IntVec3(2 + i * 2, 0, 1), map);
                    }
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(hero);
                    state = 1;
                    break;
                }
                case 1:
                {
                    // pin rotations right before the shot (pathing may have turned them)
                    var rots = new[] { Rot4.South, Rot4.East, Rot4.North };
                    for (int i = 0; i < 3; i++)
                    {
                        porters[i].jobs?.StopAll();
                        porters[i].Rotation = rots[i];
                    }
                    Find.CameraDriver.SetRootPosAndSize(hero.DrawPos + new Vector3(2f, 0f, 1f), 9f);
                    state = 2;
                    break;
                }
                case 2:
                    ScreenshotTaker.TakeNonSteamShot("rhartspike");
                    Log.Message("[RimHeroes.ArtSpike] RESULT: screenshot taken verdict=PASS");
                    state = 3;
                    break;
                case 3:
                    state = 4;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
