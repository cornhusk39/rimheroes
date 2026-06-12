using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Full-mod verification: launch with -quicktest -rhfullspike.
    /// A: 12 classes, all with vestments + 3 gestral unlocks; 8 of 10 gestral castes have races.
    /// B: class tome turns a commoner into a Bard (gating verified both ways).
    /// C: half-caster slots (Paladin L5 = 4/2); Cleric L5 revivifies a fresh corpse.
    /// D: enemy hero loot - hostile hero death drops an inlay item.
    /// E: hero corpse protection blocks cremation/butchering bills.
    /// F: every wired gestral caste spawns with its work type prioritized.
    /// </summary>
    public class GameComponent_FullSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhfullspike");

        private int state;
        private float nextStateTime = -1f;
        private bool passA, passB, passC, passD, passE, passF;

        public GameComponent_FullSpike(Game game) { }

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
            if (Find.TickManager.Paused)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            }

            switch (state)
            {
                case 0:
                {
                    var classes = DefDatabase<HeroClassDef>.AllDefsListForReading;
                    bool allComplete = classes.All(c => c.vestmentHediff != null && c.gestralUnlocks?.Count == 3);
                    int casters = classes.Count(c => c.casterProgression != CasterProgression.None);
                    int wiredCastes = DefDatabase<GestralJobDef>.AllDefsListForReading.Count(j => j.pawnKind != null);
                    passA = classes.Count == 12 && allComplete && casters == 8 && wiredCastes == 8;
                    Log.Message($"[RimHeroes.FullSpike] A: classes={classes.Count} complete={allComplete} casters={casters} wiredCastes={wiredCastes} pass={passA}");
                    state = 1;
                    break;
                }
                case 1:
                {
                    var commoner = SpawnColonist(map);
                    var tome = ThingMaker.MakeThing(ThingDef.Named("RH_Tome_Bard"));
                    GenSpawn.Spawn(tome, commoner.Position, map);
                    var effect = tome.TryGetComp<CompUseEffect_MakeHero>();
                    bool usableByCommoner = effect.CanBeUsedBy(commoner).Accepted;
                    effect.DoEffect(commoner);
                    var hediff = HeroUtility.GetHeroHediff(commoner);
                    bool isBard = hediff?.classDef?.defName == "RH_Bard";
                    bool blockedNow = !effect.CanBeUsedBy(commoner).Accepted;
                    passB = usableByCommoner && isBard && blockedNow;
                    Log.Message($"[RimHeroes.FullSpike] B(tome): usable={usableByCommoner} becameBard={isBard} blockedAfter={blockedNow} pass={passB}");
                    state = 2;
                    break;
                }
                case 2:
                {
                    var paladin = SpawnColonist(map);
                    var pHediff = HeroUtility.MakeHero(paladin, DefDatabase<HeroClassDef>.GetNamed("RH_Paladin"));
                    pHediff.SetLevelDirect(5);
                    bool halfSlots = pHediff.MaxSlots(1) == 4 && pHediff.MaxSlots(2) == 2 && pHediff.MaxSlots(3) == 0;

                    var cleric = SpawnColonist(map);
                    var cHediff = HeroUtility.MakeHero(cleric, DefDatabase<HeroClassDef>.GetNamed("RH_Cleric"));
                    cHediff.SetLevelDirect(5);
                    var victim = SpawnColonist(map);
                    victim.Kill(null);
                    var corpse = victim.Corpse;
                    var revivify = cleric.abilities.abilities.OfType<Ability_Spell>().First(a => a.def.defName == "RH_Spell_Revivify");
                    bool cast = revivify.Activate((LocalTargetInfo)corpse, (LocalTargetInfo)corpse);
                    bool alive = !victim.Dead;
                    passC = halfSlots && cast && alive;
                    Log.Message($"[RimHeroes.FullSpike] C: halfSlots(4/2/0)={halfSlots} revivifyCast={cast} targetAlive={alive} pass={passC}");
                    state = 3;
                    break;
                }
                case 3:
                {
                    var faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
                    var enemy = PawnGenerator.GeneratePawn(new PawnGenerationRequest(faction?.RandomPawnKind() ?? PawnKindDefOf.Colonist, faction));
                    GenSpawn.Spawn(enemy, map.Center + new IntVec3(10, 0, 0), map);
                    var eHediff = HeroUtility.MakeHero(enemy, DefDatabase<HeroClassDef>.GetNamed("RH_Fighter"));
                    eHediff.SetLevelDirect(6);
                    var pos = enemy.Position;
                    int itemsBefore = CountInlayItemsNear(map, pos);
                    enemy.Kill(null);
                    int itemsAfter = CountInlayItemsNear(map, pos);
                    passD = enemy.Faction != Faction.OfPlayer && itemsAfter > itemsBefore;
                    Log.Message($"[RimHeroes.FullSpike] D(loot): inlays {itemsBefore}->{itemsAfter} pass={passD}");
                    state = 4;
                    break;
                }
                case 4:
                {
                    var heroCorpse = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                        .OfType<Corpse>().FirstOrDefault(c => HeroUtility.IsHero(c.InnerPawn));
                    var commonCorpse = map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse)
                        .OfType<Corpse>().FirstOrDefault(c => !HeroUtility.IsHero(c.InnerPawn) && c.InnerPawn.RaceProps.Humanlike);
                    if (commonCorpse == null)
                    {
                        var extra = SpawnColonist(map);
                        extra.Kill(null);
                        commonCorpse = extra.Corpse;
                    }
                    var cremate = DefDatabase<RecipeDef>.GetNamed("CremateCorpse");
                    bool heroBlocked = HeroUtility.CorpseBillBlocked(cremate, heroCorpse);
                    bool commonAllowed = !HeroUtility.CorpseBillBlocked(cremate, commonCorpse);
                    RimHeroesMod.Settings.allowHeroCorpseDestruction = true;
                    bool overrideWorks = !HeroUtility.CorpseBillBlocked(cremate, heroCorpse);
                    RimHeroesMod.Settings.allowHeroCorpseDestruction = false;
                    passE = heroBlocked && commonAllowed && overrideWorks;
                    Log.Message($"[RimHeroes.FullSpike] E(corpse): heroBlocked={heroBlocked} commonAllowed={commonAllowed} settingOverride={overrideWorks} pass={passE}");
                    state = 5;
                    break;
                }
                case 5:
                {
                    int ok = 0;
                    var wired = DefDatabase<GestralJobDef>.AllDefsListForReading.Where(j => j.pawnKind != null).ToList();
                    foreach (var job in wired)
                    {
                        var gestral = PawnGenerator.GeneratePawn(new PawnGenerationRequest(job.pawnKind, Faction.OfPlayer));
                        CellFinder.TryFindRandomCellNear(map.Center, map, 12, c => c.Standable(map), out var cell);
                        GenSpawn.Spawn(gestral, cell, map);
                        var comp = gestral.TryGetComp<CompGestralWorker>();
                        bool workOk = comp == null // combat castes have no worker comp
                                      || comp.Props.workTypes.All(w => gestral.workSettings?.GetPriority(w) == 1);
                        if (gestral.Spawned && workOk)
                        {
                            ok++;
                        }
                        else
                        {
                            Log.Message($"[RimHeroes.FullSpike] F: caste {job.defName} failed (spawned={gestral.Spawned} workOk={workOk})");
                        }
                    }
                    passF = ok == wired.Count && wired.Count == 8;
                    Log.Message($"[RimHeroes.FullSpike] F(castes): {ok}/{wired.Count} ok pass={passF}");

                    string verdict = passA && passB && passC && passD && passE && passF ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.FullSpike] RESULT: A={passA} B={passB} C={passC} D={passD} E={passE} F={passF} verdict={verdict}");
                    state = 6;
                    break;
                }
                case 6:
                    state = 7;
                    Root.Shutdown();
                    break;
            }
        }

        private static int CountInlayItemsNear(Map map, IntVec3 pos)
        {
            return map.listerThings.AllThings.Count(t =>
                t.def.defName.StartsWith("RH_InlayItem_") && t.Position.InHorDistOf(pos, 6f));
        }

        private Pawn SpawnColonist(Map map)
        {
            var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
            CellFinder.TryFindRandomCellNear(map.Center, map, 10, c => c.Standable(map), out var cell);
            GenSpawn.Spawn(pawn, cell, map);
            return pawn;
        }
    }
}
