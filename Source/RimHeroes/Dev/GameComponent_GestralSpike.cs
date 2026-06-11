using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated spike test: launch the game with -quicktest -rhspike and this component
    /// spawns a Sweeper test gestral amid filth, then logs whether it actually cleans.
    /// Inert without the -rhspike command-line arg. Shuts the game down when finished.
    /// </summary>
    public class GameComponent_GestralSpike : GameComponent
    {
        private const int SettleTicks = 600;
        private const int TestDurationTicks = 7500; // 3 in-game hours
        private const float TestRadius = 10f;

        private int state;
        private int startTick = -1;
        private int startFilth;
        private Pawn sweeper;
        private IntVec3 center;
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhspike");

        public GameComponent_GestralSpike(Game game) { }

        public override void GameComponentTick()
        {
            if (!Active || state > 1)
            {
                return;
            }
            var map = Find.CurrentMap;
            if (map == null)
            {
                return;
            }
            int tick = Find.TickManager.TicksGame;
            if (state == 0)
            {
                if (tick < SettleTicks)
                {
                    return;
                }
                Setup(map);
                startTick = tick;
                state = 1;
                return;
            }
            int elapsed = tick - startTick;
            if (elapsed % 2500 == 0)
            {
                Log.Message($"[RimHeroes.Spike] t+{elapsed}: job={sweeper?.CurJobDef?.defName ?? "none"}, filth={CountFilth(map)}");
            }
            if (elapsed >= TestDurationTicks)
            {
                int remaining = CountFilth(map);
                int cleaned = startFilth - remaining;
                string verdict = cleaned >= 5 ? "PASS" : "FAIL";
                Log.Message($"[RimHeroes.Spike] RESULT: start={startFilth} remaining={remaining} cleaned={cleaned} verdict={verdict}");
                state = 2;
                Root.Shutdown();
            }
        }

        private void Setup(Map map)
        {
            if (!CellFinder.TryFindRandomCellNear(map.Center, map, 10, c => c.Standable(map) && !c.Fogged(map), out center))
            {
                center = map.Center;
            }
            var dirt = ThingDefOf.Filth_Blood;
            int made = 0;
            foreach (var c in GenRadial.RadialCellsAround(center, 6f, useCenter: true))
            {
                if (!c.InBounds(map) || !c.Walkable(map))
                {
                    continue;
                }
                map.areaManager.Home[c] = true;
                if (made < 30 && FilthMaker.TryMakeFilth(c, map, dirt))
                {
                    made++;
                }
            }
            startFilth = CountFilth(map);

            var kind = PawnKindDef.Named("RH_GestralSweeperKind");
            sweeper = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, Faction.OfPlayer));
            GenSpawn.Spawn(sweeper, center, map);
            if (sweeper.needs?.food != null) sweeper.needs.food.CurLevelPercentage = 1f;
            if (sweeper.needs?.rest != null) sweeper.needs.rest.CurLevelPercentage = 1f;
            Log.Message($"[RimHeroes.Spike] setup: spawned {sweeper.LabelShort} at {center}, filth={startFilth}, workSettings={(sweeper.workSettings != null && sweeper.workSettings.EverWork ? "initialized" : "MISSING")}, cleaningPriority={sweeper.workSettings?.GetPriority(WorkTypeDefOf.Cleaning) ?? -1}");
        }

        private int CountFilth(Map map)
        {
            var dirt = ThingDefOf.Filth_Blood;
            return map.listerThings.ThingsOfDef(dirt).Count(t => t.Position.InHorDistOf(center, TestRadius));
        }
    }
}
