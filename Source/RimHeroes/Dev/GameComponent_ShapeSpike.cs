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
                    // Spawn the whole bestiary; verify biome lists carry each kind somewhere.
                    string[] kinds = { "RH_OwlbearKind", "RH_DireWolfKind", "RH_GiantElkKind", "RH_AnkhegKind", "RH_BasiliskKind" };
                    int spawned = 0;
                    foreach (var kindName in kinds)
                    {
                        var kind = PawnKindDef.Named(kindName);
                        var beast = PawnGenerator.GeneratePawn(kind);
                        CellFinder.TryFindRandomCellNear(map.Center + new IntVec3(15, 0, 15), map, 12, c => c.Standable(map), out var cell);
                        GenSpawn.Spawn(beast, cell, map);
                        if (beast.Spawned)
                        {
                            spawned++;
                        }
                    }
                    bool inBiomes =
                        DefDatabase<BiomeDef>.GetNamed("TemperateForest").AllWildAnimals.Contains(PawnKindDef.Named("RH_OwlbearKind"))
                        && DefDatabase<BiomeDef>.GetNamed("BorealForest").AllWildAnimals.Contains(PawnKindDef.Named("RH_DireWolfKind"))
                        && DefDatabase<BiomeDef>.GetNamed("Tundra").AllWildAnimals.Contains(PawnKindDef.Named("RH_GiantElkKind"))
                        && DefDatabase<BiomeDef>.GetNamed("AridShrubland").AllWildAnimals.Contains(PawnKindDef.Named("RH_AnkhegKind"))
                        && DefDatabase<BiomeDef>.GetNamed("Desert").AllWildAnimals.Contains(PawnKindDef.Named("RH_BasiliskKind"));
                    passA = spawned == kinds.Length && inBiomes;
                    Log.Message($"[RimHeroes.ShapeSpike] PhaseA: beasts spawned={spawned}/{kinds.Length} biomes={inBiomes} pass={passA}");
                    state = 1;
                    break;
                }
                case 1:
                {
                    // dedicated isolated druid on cleared sand, away from the quicktest base, so the
                    // auto-hero starting colonists don't get confused with our test subject
                    var spot = map.Center + new IntVec3(28, 0, 0);
                    for (int dx = -6; dx <= 6; dx++)
                        for (int dz = -6; dz <= 6; dz++)
                        {
                            var c = spot + new IntVec3(dx, 0, dz);
                            if (!c.InBounds(map)) continue;
                            foreach (var t in c.GetThingList(map).ToList())
                                if (!(t is Pawn) && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                            map.terrainGrid.SetTerrain(c, TerrainDefOf.Sand);
                        }
                    hero = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    GenSpawn.Spawn(hero, spot, map);
                    var levels = HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Druid"));
                    levels.GainXP(1300f); // L8: dire wolf (2), owlbear (5), giant elk (8)
                    baseMoveSpeed = hero.GetStatValue(StatDefOf.MoveSpeed);
                    int formCount = hero.abilities.abilities.Count(a => a.def.defName.StartsWith("RH_Ability_Wildshape"));

                    // Cast dire wolf, then owlbear: exclusivity must leave exactly one form (owlbear).
                    var wolf = hero.abilities.abilities.OfType<Ability_Spell>().First(a => a.def.defName == "RH_Ability_WildshapeDireWolf");
                    wolf.Activate(hero, hero);
                    bool wolfOn = hero.health.hediffSet.HasHediff(HediffDef.Named("RH_WildshapeDireWolf"));
                    var owlbearForm = hero.abilities.abilities.OfType<Ability_Spell>().First(a => a.def.defName == "RH_Ability_WildshapeOwlbear");
                    bool activated = owlbearForm.Activate(hero, hero);
                    int activeForms = hero.health.hediffSet.hediffs.Count(h => h is Hediff_Wildshape);
                    bool wolfReplaced = !hero.health.hediffSet.HasHediff(HediffDef.Named("RH_WildshapeDireWolf"))
                                        && hero.health.hediffSet.HasHediff(HediffDef.Named("RH_WildshapeOwlbear"));

                    var form = hero.health.hediffSet.hediffs.OfType<Hediff_Wildshape>().FirstOrDefault();
                    var verbs = (form as HediffWithComps)?.TryGetComp<HediffComp_VerbGiver>()?.VerbTracker?.AllVerbs;
                    float shiftedSpeed = hero.GetStatValue(StatDefOf.MoveSpeed);
                    passB = formCount == 3 && wolfOn && activated && activeForms == 1 && wolfReplaced
                            && verbs != null && verbs.Count >= 2 && shiftedSpeed > baseMoveSpeed + 0.5f;
                    Log.Message($"[RimHeroes.ShapeSpike] PhaseB: level={levels.level} forms={formCount}/3 wolfFirst={wolfOn} owlbearCast={activated} activeForms={activeForms} replaced={wolfReplaced} naturalWeapons={verbs?.Count ?? 0} move {baseMoveSpeed:F1}->{shiftedSpeed:F1} pass={passB}");
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(hero);
                    state = 2;
                    break;
                }
                case 2:
                {
                    // close every open window (dev log + auto class-pick dialogs) so they don't photobomb
                    foreach (var w in Find.WindowStack.Windows.ToList())
                    {
                        if (!(w is MainTabWindow)) w.Close(false);
                    }
                    Messages.Clear();
                    hero.jobs?.StopAll();
                    Find.CameraDriver.SetRootPosAndSize(hero.Position.ToVector3Shifted(), 7f); // re-assert right before the shot
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
