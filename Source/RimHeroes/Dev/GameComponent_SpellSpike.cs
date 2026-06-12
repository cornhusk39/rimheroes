using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated spell-engine test: launch with -quicktest -rhspellspike.
    /// Phase A: Wizard L5 -> 4 spells granted, slot table 4/3/2, fire bolt cantrip autocast-enabled.
    /// Phase B: cast Magic Missile (slot spent, target damaged), drain L1 slots -> gizmo disabled.
    /// Phase C: short rest restores one slot; long rest restores all.
    /// Phase D: autocast - drafted wizard + hostile -> fire bolt cast happens without orders.
    /// </summary>
    public class GameComponent_SpellSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhspellspike");

        private int state;
        private int attempts;
        private float nextStateTime = -1f;
        private Pawn hero;
        private Pawn victim;
        private bool passA, passB, passC, passD;

        public GameComponent_SpellSpike(Game game) { }

        private Hediff_HeroLevels Hero => HeroUtility.GetHeroHediff(hero);

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
                    hero = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                    if (hero == null)
                    {
                        hero = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(hero, map.Center, map);
                    }
                    var levels = HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Wizard"));
                    levels.GainXP(600f); // L5
                    int spells = hero.abilities.abilities.Count(a => a is Ability_Spell);
                    bool slots = levels.MaxSlots(1) == 4 && levels.MaxSlots(2) == 3 && levels.MaxSlots(3) == 2 && levels.MaxSlots(4) == 0;
                    bool cantripAuto = levels.AutocastEnabled(AbilityDefOf());
                    passA = spells == 4 && slots && cantripAuto;
                    Log.Message($"[RimHeroes.SpellSpike] PhaseA: level={levels.level} spells={spells} slots(4/3/2)={slots} cantripAutocast={cantripAuto} pass={passA}");
                    state = 1;
                    break;
                }
                case 1:
                {
                    var levels = Hero;
                    victim = SpawnVictim(map, hero.Position + new IntVec3(6, 0, 0));
                    float hpBefore = victim.health.summaryHealth.SummaryHealthPercent;
                    var missile = GetSpell("RH_Spell_MagicMissile");
                    bool activated = missile.Activate(victim, victim);
                    float hpAfter = victim.health.summaryHealth.SummaryHealthPercent;
                    bool slotSpent = levels.RemainingSlots(1) == 3;
                    // drain remaining L1 slots
                    levels.TryExpendSlot(1);
                    levels.TryExpendSlot(1);
                    levels.TryExpendSlot(1);
                    bool drained = levels.RemainingSlots(1) == 0;
                    bool gizmoBlocked = missile.GizmoDisabled(out var reason);
                    bool cantripFine = !GetSpell("RH_Spell_FireBolt").GizmoDisabled(out _);
                    passB = activated && hpAfter < hpBefore && slotSpent && drained && gizmoBlocked && cantripFine;
                    Log.Message($"[RimHeroes.SpellSpike] PhaseB: cast={activated} dmg={hpBefore:F2}->{hpAfter:F2} slotSpent={slotSpent} drained={drained} blocked={gizmoBlocked}('{reason}') cantripOk={cantripFine} pass={passB}");
                    state = 2;
                    break;
                }
                case 2:
                {
                    var levels = Hero;
                    // short rest: simulate via the same method the sleep hook calls
                    bool restored = levels.RestoreLowestExpendedSlot();
                    bool oneBack = levels.RemainingSlots(1) == 1;
                    // long rest completion path
                    levels.TryExpendSlot(3);
                    levels.RefreshAllSlots();
                    bool allBack = levels.RemainingSlots(1) == 4 && levels.RemainingSlots(2) == 3 && levels.RemainingSlots(3) == 2;
                    passC = restored && oneBack && allBack;
                    Log.Message($"[RimHeroes.SpellSpike] PhaseC: shortRest={restored} oneBack={oneBack} longRestAllBack={allBack} pass={passC}");
                    // prep autocast phase: draft hero, fresh hostile in range
                    if (victim == null || victim.Dead)
                    {
                        victim = SpawnVictim(map, hero.Position + new IntVec3(8, 0, 2));
                    }
                    hero.drafter.Drafted = true;
                    state = 3;
                    attempts = 0;
                    break;
                }
                case 3: // wait for autocast to fire bolt the hostile
                {
                    bool casting = hero.CurJob?.ability != null;
                    bool victimHurt = victim.Dead || victim.health.summaryHealth.SummaryHealthPercent < 0.999f;
                    if (casting || victimHurt)
                    {
                        passD = true;
                        Log.Message($"[RimHeroes.SpellSpike] PhaseD: autocast fired (casting={casting} victimHurt={victimHurt}) pass=True");
                        state = 4;
                    }
                    else if (++attempts > 10)
                    {
                        Log.Message($"[RimHeroes.SpellSpike] PhaseD: autocast never fired (job={hero.CurJobDef?.defName}) pass=False");
                        state = 4;
                    }
                    break;
                }
                case 4:
                {
                    string verdict = passA && passB && passC && passD ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.SpellSpike] RESULT: A={passA} B={passB} C={passC} D={passD} verdict={verdict}");
                    state = 5;
                    break;
                }
                case 5:
                    state = 6;
                    Root.Shutdown();
                    break;
            }
        }

        private Ability_Spell GetSpell(string defName) =>
            hero.abilities.abilities.OfType<Ability_Spell>().First(a => a.def.defName == defName);

        private static AbilityDef AbilityDefOf() => DefDatabase<AbilityDef>.GetNamed("RH_Spell_FireBolt");

        private Pawn SpawnVictim(Map map, IntVec3 pos)
        {
            var faction = Find.FactionManager.AllFactions.FirstOrDefault(f => f.HostileTo(Faction.OfPlayer) && f.def.humanlikeFaction);
            var kind = faction?.RandomPawnKind() ?? PawnKindDefOf.Colonist;
            var pawn = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction));
            if (!pos.InBounds(map) || !pos.Standable(map))
            {
                CellFinder.TryFindRandomCellNear(hero.Position, map, 8, c => c.Standable(map), out pos);
            }
            GenSpawn.Spawn(pawn, pos, map);
            return pawn;
        }
    }
}
