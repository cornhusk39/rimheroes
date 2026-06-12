using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated wildshape + owlbear test: launch with -quicktest -rhshapespike.
    /// Phase A: owlbear race loads, biome spawn lists include it, a wild one spawns with sane stats.
    /// Phase B: Druid L5 has wildshape; casting it applies the form (verbs, stats, hidden gear).
    /// Phase C: revert removes the form and restores stats.
    /// Screenshots the shifted druid for visual confirmation.
    /// </summary>
    public class GameComponent_ShapeSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhshapespike");

        private int state;
        private float nextStateTime = -1f;
        private Pawn hero;
        private float baseMoveSpeed;
        private bool passA, passB, passC;

        public GameComponent_ShapeSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 4)
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
                    var kind = PawnKindDef.Named("RH_OwlbearKind");
                    var owlbear = PawnGenerator.GeneratePawn(kind);
                    CellFinder.TryFindRandomCellNear(map.Center + new IntVec3(15, 0, 15), map, 10, c => c.Standable(map), out var cell);
                    GenSpawn.Spawn(owlbear, cell, map);
                    bool inBiome = DefDatabase<BiomeDef>.GetNamed("TemperateForest").AllWildAnimals.Contains(kind)
                                   && DefDatabase<BiomeDef>.GetNamed("BorealForest").AllWildAnimals.Contains(kind);
                    bool statsSane = owlbear.RaceProps.baseBodySize > 2f && owlbear.RaceProps.predator && kind.combatPower >= 200;
                    passA = owlbear.Spawned && inBiome && statsSane;
                    Log.Message($"[RimHeroes.ShapeSpike] PhaseA: owlbear spawned={owlbear.Spawned} biomes={inBiome} stats(body={owlbear.RaceProps.baseBodySize},pred={owlbear.RaceProps.predator},cp={kind.combatPower}) pass={passA}");
                    state = 1;
                    break;
                }
                case 1:
                {
                    hero = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                    if (hero == null)
                    {
                        hero = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(hero, map.Center, map);
                    }
                    var levels = HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Druid"));
                    levels.GainXP(600f); // L5
                    baseMoveSpeed = hero.GetStatValue(StatDefOf.MoveSpeed);
                    var wildshape = hero.abilities.abilities.OfType<Ability_Spell>()
                        .FirstOrDefault(a => a.def.defName == "RH_Ability_WildshapeOwlbear");
                    bool activated = wildshape != null && wildshape.Activate(hero, hero);
                    var form = hero.health.hediffSet.hediffs.OfType<Hediff_Wildshape>().FirstOrDefault();
                    var verbs = (form as HediffWithComps)?.TryGetComp<HediffComp_VerbGiver>()?.VerbTracker?.AllVerbs;
                    float shiftedSpeed = hero.GetStatValue(StatDefOf.MoveSpeed);
                    passB = activated && form != null && verbs != null && verbs.Count >= 2 && shiftedSpeed > baseMoveSpeed + 0.5f;
                    Log.Message($"[RimHeroes.ShapeSpike] PhaseB: cast={activated} form={(form != null)} naturalWeapons={verbs?.Count ?? 0} move {baseMoveSpeed:F1}->{shiftedSpeed:F1} pass={passB}");
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(hero);
                    state = 2;
                    break;
                }
                case 2:
                {
                    Find.CameraDriver.SetRootPosAndSize(hero.DrawPos, 7f); // re-assert right before the shot
                    nextStateTime = Time.realtimeSinceStartup + 1.5f;
                    ScreenshotTaker.TakeNonSteamShot("rhshapespike");
                    state = 3;
                    break;
                }
                case 3:
                {
                    var form = hero.health.hediffSet.hediffs.OfType<Hediff_Wildshape>().FirstOrDefault();
                    if (form != null)
                    {
                        hero.health.RemoveHediff(form); // the revert gizmo's action
                    }
                    bool gone = !Hediff_Wildshape.IsShifted(hero);
                    float revertedSpeed = hero.GetStatValue(StatDefOf.MoveSpeed);
                    passC = gone && Mathf.Abs(revertedSpeed - baseMoveSpeed) < 0.05f;
                    string verdict = passA && passB && passC ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.ShapeSpike] PhaseC: reverted={gone} move={revertedSpeed:F1} pass={passC}");
                    Log.Message($"[RimHeroes.ShapeSpike] RESULT: A={passA} B={passB} C={passC} verdict={verdict}");
                    state = 4;
                    break;
                }
                case 4:
                    state = 5;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
