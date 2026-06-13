using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Aura showcase: launch with -quicktest -rhaurademo [-rhvestclass=RH_Sorcerer].
    /// Spawns one L20 hero of the class, then takes a burst of screenshots WITHOUT pausing so the
    /// pulsing glow and drifting motes animate across frames, then exits. The frames are assembled
    /// into a filmstrip offline (tools/make-aura-strip.mjs).
    /// </summary>
    public class GameComponent_AuraDemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhaurademo");

        private const int Shots = 12;
        private int state;
        private float nextStateTime = -1f;
        private Pawn pawn;
        private IntVec3 anchor;

        public GameComponent_AuraDemoSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > Shots + 1)
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

            if (state == 0)
            {
                string className = "RH_Sorcerer";
                GenCommandLine.TryGetCommandLineArg("rhvestclass", out var cls);
                if (!cls.NullOrEmpty()) className = cls;
                var classDef = DefDatabase<HeroClassDef>.GetNamed(className);
                var searchFrom = map.Center + new IntVec3(0, 0, -35);
                CellFinder.TryFindRandomCellNear(searchFrom, map, 10, c => c.Standable(map), out anchor);
                try
                {
                    pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                        fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true, fixedGender: Gender.Male));
                    GenSpawn.Spawn(pawn, anchor, map);
                    pawn.apparel?.DestroyAll();
                    if (pawn.story != null) pawn.story.bodyType = BodyTypeDefOf.Male;
                    var levels = HeroUtility.MakeHero(pawn, classDef);
                    levels.SetLevelDirect(20);
                    pawn.Drawer?.renderer?.SetAllGraphicsDirty();
                    if (pawn.drafter != null) pawn.drafter.Drafted = true;
                }
                catch (System.Exception e)
                {
                    Log.Error($"[RimHeroes.AuraDemo] spawn failed: {e}");
                }
                // keep time running (do NOT pause) so motes emit and the glow pulses
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 6f);
                nextStateTime = now + 1.5f;
                state = 1;
                return;
            }

            if (state >= 1 && state <= Shots)
            {
                if (pawn != null)
                {
                    pawn.jobs?.StopAll();
                    pawn.Rotation = Rot4.South;
                }
                Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 6f);
                ScreenshotTaker.TakeNonSteamShot($"rhaura_{state - 1:00}");
                nextStateTime = now + 0.22f;
                state++;
                return;
            }

            Log.Message("[RimHeroes.AuraDemo] RESULT: aura demo frames taken verdict=PASS");
            state = Shots + 2;
            Root.Shutdown();
        }
    }
}
