using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Vanilla apparel reference capture: launch with -quicktest -rhapparelspike. Spawns five
    /// colonists dressed in escalating vanilla outfits, pins them south, screenshots, exits.
    /// </summary>
    public class GameComponent_ApparelRefSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhapparelspike");

        private int state;
        private float nextStateTime = -1f;
        private Pawn[] pawns;
        private IntVec3 anchor;

        public GameComponent_ApparelRefSpike(Game game) { }

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
                    CellFinder.TryFindRandomCellNear(map.Center, map, 15, c => c.Standable(map), out anchor);
                    string[][] outfits =
                    {
                        new[] { "Apparel_TribalA" },
                        new[] { "Apparel_BasicShirt", "Apparel_Pants" },
                        new[] { "Apparel_BasicShirt", "Apparel_Pants", "Apparel_FlakVest" },
                        new[] { "Apparel_PlateArmor" },
                        new[] { "Apparel_PowerArmor" },
                    };
                    pawns = new Pawn[outfits.Length];
                    for (int i = 0; i < outfits.Length; i++)
                    {
                        try
                        {
                            Log.Message($"[RimHeroes.ApparelRefSpike] generating pawn {i}");
                            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                                fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true));
                            pawns[i] = p;
                            GenSpawn.Spawn(p, anchor + new IntVec3(i * 3 - 6, 0, 0), map);
                            p.apparel?.DestroyAll();
                            foreach (var defName in outfits[i])
                            {
                                Log.Message($"[RimHeroes.ApparelRefSpike] pawn {i} wearing {defName}");
                                var def = ThingDef.Named(defName);
                                var stuff = def.MadeFromStuff ? GenStuff.DefaultStuffFor(def) : null;
                                p.apparel.Wear((Apparel)ThingMaker.MakeThing(def, stuff), false);
                            }
                            if (p.drafter != null)
                            {
                                p.drafter.Drafted = true;
                            }
                        }
                        catch (System.Exception e)
                        {
                            Log.Error($"[RimHeroes.ApparelRefSpike] pawn {i} failed: {e}");
                        }
                    }
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 11f);
                    state = 1;
                    break;
                }
                case 1:
                    Find.TickManager.Pause();
                    foreach (var p in pawns)
                    {
                        if (p == null) continue;
                        p.jobs?.StopAll();
                        p.Rotation = Rot4.South;
                    }
                    ScreenshotTaker.TakeNonSteamShot("rhapparel_south");
                    state = 2;
                    break;
                case 2:
                    Log.Message("[RimHeroes.ApparelRefSpike] RESULT: apparel reference shot taken verdict=PASS");
                    state = 3;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
