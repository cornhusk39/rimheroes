using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>Marks a projectile def as resolving with a spell attack roll. Tagged spells can miss;
    /// untagged ones (Magic Missile, saving-throw spells, AoE, buffs, heals) always land. The beam comp
    /// carries the same flag directly on its props (see CompProperties_AbilityBeam.attackRoll).</summary>
    public class SpellAccuracyExtension : DefModExtension
    {
        public bool attackRoll = false;
    }

    /// <summary>
    /// Hybrid 5e spell accuracy. "Attack" spells (Fire Bolt, Ray of Frost, Eldritch Blast, Scorching
    /// Ray, Shocking Grasp, Chaos Bolt, Chromatic Orb, Produce Flame) roll d20 + the caster's spell
    /// attack bonus against the target's armor class and can miss. Everything else lands automatically.
    /// The attack bonus rides the existing Arcane check (Intellectual + Spell Power + caster class
    /// affinity + traits) plus a 5e proficiency bonus that grows with hero level, so a stronger caster
    /// lands more of its bolts.
    /// </summary>
    public static class SpellAccuracy
    {
        // A throwaway Arcane check: CheckMods reads only its kind/flatModifier, so no def registration
        // is needed to reuse the same modifier math the dungeon d20 checks use.
        private static readonly CheckDef ArcaneAttack = new CheckDef { kind = CheckKind.Arcane };

        public static bool UsesAttackRoll(Def def) =>
            def?.GetModExtension<SpellAccuracyExtension>()?.attackRoll ?? false;

        /// <summary>d20 + spell attack bonus vs the target's armor class. Natural 20 always hits and
        /// natural 1 always misses (the d20 buckets handle the crits).</summary>
        public static bool Hits(Pawn caster, Pawn target)
        {
            if (caster == null || target == null) return true;
            return D20.Roll(AttackBonus(caster), ArmorClass(target)).Passed;
        }

        public static int AttackBonus(Pawn caster) =>
            CheckMods.GetModifier(caster, ArcaneAttack) + ProficiencyBonus(caster);

        // 5e proficiency: +2 at levels 1-4, climbing to +6 by level 17.
        private static int ProficiencyBonus(Pawn caster)
        {
            int lvl = HeroUtility.GetHeroHediff(caster)?.level ?? 1;
            return 2 + Mathf.Clamp((lvl - 1) / 4, 0, 4);
        }

        // Target "armor class": base 11, plus a Dexterity-like dodge from move speed and a bonus from
        // worn armor. Unarmored and slow lands near 11; fast or heavily armored climbs toward the high
        // teens, the same 11-18 spread a 5e target spans.
        public static int ArmorClass(Pawn target)
        {
            if (target == null) return 10;
            int dodge = Mathf.Clamp(Mathf.RoundToInt((target.GetStatValue(StatDefOf.MoveSpeed) - 4.6f) * 2f), -2, 6);
            int armor = Mathf.Clamp(Mathf.RoundToInt(target.GetStatValue(StatDefOf.ArmorRating_Sharp) * 6f), 0, 6);
            return 11 + dodge + armor;
        }

        public static void ThrowMiss(Pawn target)
        {
            if (target?.Map == null || !target.Spawned) return;
            MoteMaker.ThrowText(target.DrawPos, target.Map, "RH_SpellMiss".Translate(), new Color(0.8f, 0.8f, 0.85f), 1.9f);
        }
    }
}
