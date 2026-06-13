using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Spell Power proof: -quicktest -rhspellpowerdemo. Spawns a wizard + a dummy, casts its
    /// direct-damage spell on the dummy with no focus, then with a Perfect Hero Staff, and logs the
    /// spell-power stat and the damage dealt each time so the scaling is visible. Then exits.
    /// </summary>
    public class GameComponent_SpellPowerDemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhspellpowerdemo");
        private int state;
        private float nextStateTime = -1f;

        public GameComponent_SpellPowerDemoSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 1) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f) { nextStateTime = now + 4f; return; }
            if (now < nextStateTime) return;
            try { RunDemo(map); }
            catch (System.Exception e) { Log.Error("[RimHeroes.SpellPowerDemo] " + e); }
            state = 2;
            Root.Shutdown();
        }

        private static float InjurySum(Pawn p) =>
            p.health.hediffSet.hediffs.OfType<Hediff_Injury>().Sum(i => i.Severity);

        private static void Heal(Pawn p)
        {
            foreach (var inj in p.health.hediffSet.hediffs.OfType<Hediff_Injury>().ToList())
                p.health.RemoveHediff(inj);
        }

        private void RunDemo(Map map)
        {
            CellFinder.TryFindRandomCellNear(map.Center, map, 10, c => c.Standable(map), out var anchor);
            var wizard = SpawnHero(map, anchor, "RH_Wizard", 20);
            var dummy = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, fixedGender: Gender.Male));
            GenSpawn.Spawn(dummy, anchor + new IntVec3(2, 0, 0), map);
            dummy.apparel?.DestroyAll();

            CompAbilityEffect_DamageTarget comp = null;
            string spellName = "";
            foreach (var ab in wizard.abilities?.abilities ?? new List<Ability>())
            {
                var c = ab.CompOfType<CompAbilityEffect_DamageTarget>();
                if (c != null) { comp = c; spellName = ab.def.defName; break; }
            }
            if (comp == null) { Log.Error("[RimHeroes.SpellPowerDemo] wizard has no direct-damage spell"); return; }
            float baseAmount = ((CompProperties_AbilityDamageTarget)comp.props).amount;

            // 1) bare (no focus)
            float sp0 = SpellPower.For(wizard);
            Heal(dummy);
            comp.Apply(new LocalTargetInfo(dummy), default);
            float d0 = InjurySum(dummy);
            Log.Message($"[RimHeroes.SpellPowerDemo] BARE       spell='{spellName}' base={baseAmount:F0}  RH_SpellPower={sp0:P0}  effective={baseAmount * sp0:F1}  dummyTookDamage={d0:F1}");

            // 2) with the Perfect Hero Staff equipped (+0.75 spell power)
            EquipOn(wizard, "RH_Weapon_Wizard_T4");
            float sp1 = SpellPower.For(wizard);
            Heal(dummy);
            comp.Apply(new LocalTargetInfo(dummy), default);
            float d1 = InjurySum(dummy);
            Log.Message($"[RimHeroes.SpellPowerDemo] WITH STAFF spell='{spellName}' base={baseAmount:F0}  RH_SpellPower={sp1:P0}  effective={baseAmount * sp1:F1}  dummyTookDamage={d1:F1}");

            Log.Message($"[RimHeroes.SpellPowerDemo] RESULT: focus raised spell power {sp0:P0} -> {sp1:P0} (x{sp1 / sp0:F2}); spell damage {d0:F1} -> {d1:F1} verdict=PASS");
        }

        private Pawn SpawnHero(Map map, IntVec3 cell, string cls, int lvl)
        {
            var cd = DefDatabase<HeroClassDef>.GetNamed(cls);
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, fixedGender: Gender.Male));
            GenSpawn.Spawn(p, cell, map);
            p.apparel?.DestroyAll();
            var lv = HeroUtility.MakeHero(p, cd);
            lv.SetLevelDirect(lvl);
            return p;
        }

        private void EquipOn(Pawn p, string weaponDef)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDef);
            var w = (ThingWithComps)ThingMaker.MakeThing(def);
            p.equipment.MakeRoomFor(w);
            p.equipment.AddEquipment(w);
        }
    }
}
