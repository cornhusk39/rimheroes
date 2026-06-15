using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Computes a d20 check's total modifier with no new game stats: a base from the relevant vanilla
    /// skill/stat for the check's kind, plus a class-affinity bonus, plus vanilla-trait modifiers.
    /// </summary>
    public static class CheckMods
    {
        public static int GetModifier(Pawn pawn, CheckDef check)
        {
            if (pawn == null || check == null) return 0;
            return check.flatModifier + BaseMod(pawn, check) + ClassAffinity(pawn, check.kind) + TraitBonus(pawn, check.kind);
        }

        // ===== Base: the closest vanilla skill/stat per kind =====
        private static int BaseMod(Pawn pawn, CheckDef check)
        {
            switch (check.kind)
            {
                case CheckKind.Lockpick:   return SkillMod(pawn, SkillDefOf.Intellectual);
                case CheckKind.Arcane:     return SkillMod(pawn, SkillDefOf.Intellectual) + SpellPowerMod(pawn);
                case CheckKind.Perception: return SkillMod(pawn, SkillDefOf.Intellectual) + SightMod(pawn);
                case CheckKind.Dodge:      return DodgeMod(pawn);
                case CheckKind.Strength:   return SkillMod(pawn, SkillDefOf.Melee);
                case CheckKind.Social:     return SkillMod(pawn, SkillDefOf.Social);
                case CheckKind.Endurance:  return EnduranceMod(pawn);
                default:                   return SkillMod(pawn, check.skill);
            }
        }

        // 5e-style: floor((level - 8) / 2). RimWorld 0-20 skill -> roughly -4..+6.
        private static int SkillMod(Pawn pawn, SkillDef skill)
        {
            if (skill == null || pawn.skills == null) return 0;
            return Mathf.FloorToInt((pawn.skills.GetSkill(skill).Level - 8) / 2f);
        }

        private static int SpellPowerMod(Pawn pawn)
        {
            var sp = DefDatabase<StatDef>.GetNamedSilentFail("RH_SpellPower");
            return sp == null ? 0 : Mathf.RoundToInt((pawn.GetStatValue(sp) - 1f) * 4f);
        }

        private static int SightMod(Pawn pawn)
        {
            float s = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Sight) ?? 1f;
            return Mathf.RoundToInt((s - 1f) * 5f);   // bionic eyes help, blindness hurts
        }

        private static int DodgeMod(Pawn pawn)
        {
            return Mathf.RoundToInt((pawn.GetStatValue(StatDefOf.MoveSpeed) - 4.6f) * 2f);   // base human ~4.6
        }

        private static int EnduranceMod(Pawn pawn)
        {
            float bp = pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.BloodPumping) ?? 1f;
            return Mathf.RoundToInt((bp - 1f) * 2f);
        }

        // ===== Class affinity (+2 if a favored class makes the roll) =====
        private static int ClassAffinity(Pawn pawn, CheckKind kind)
        {
            string cls = HeroUtility.GetHeroHediff(pawn)?.classDef?.defName;
            if (cls == null) return 0;
            foreach (var fav in Favored(kind)) if (fav == cls) return 2;
            return 0;
        }

        private static string[] Favored(CheckKind kind)
        {
            switch (kind)
            {
                case CheckKind.Lockpick:   return new[] { "RH_Rogue" };
                case CheckKind.Arcane:     return new[] { "RH_Wizard", "RH_Cleric", "RH_Sorcerer", "RH_Warlock" };
                case CheckKind.Perception: return new[] { "RH_Ranger", "RH_Druid", "RH_Cleric" };
                case CheckKind.Dodge:      return new[] { "RH_Rogue", "RH_Monk", "RH_Ranger" };
                case CheckKind.Strength:   return new[] { "RH_Barbarian", "RH_Fighter", "RH_Paladin" };
                case CheckKind.Social:     return new[] { "RH_Bard", "RH_Paladin", "RH_Sorcerer" };
                case CheckKind.Endurance:  return new[] { "RH_Barbarian", "RH_Fighter" };
                default:                   return System.Array.Empty<string>();
            }
        }

        // ===== Vanilla trait modifiers (the rest of vanilla traits map to nothing) =====
        private static int TraitBonus(Pawn pawn, CheckKind kind)
        {
            var t = pawn.story?.traits;
            if (t == null) return 0;
            int b = 0;
            switch (kind)
            {
                case CheckKind.Dodge:
                    if (Has(t, "Nimble")) b += 3;
                    break;
                case CheckKind.Perception:
                    if (Has(t, "TooSmart")) b += 2;
                    if (Has(t, "Undergrounder")) b += 2;     // at home in the dark
                    break;
                case CheckKind.Lockpick:
                    if (Has(t, "TooSmart")) b += 2;
                    break;
                case CheckKind.Arcane:
                    if (Has(t, "TooSmart")) b += 2;
                    b += Degree(t, "PsychicSensitivity");    // hypersensitive +2 ... deaf -2
                    break;
                case CheckKind.Strength:
                    if (Has(t, "Brawler")) b += 1;
                    if (Has(t, "Tough")) b += 1;
                    break;
                case CheckKind.Endurance:
                    if (Has(t, "Tough")) b += 3;
                    if (Has(t, "Wimp")) b -= 2;
                    b += Degree(t, "Immunity") * 2;          // super-immune +2 / sickly -2
                    b += Degree(t, "Nerves");                // iron-willed +2 / steadfast +1 / nervous -1 / volatile -2
                    break;
                case CheckKind.Social:
                    if (Has(t, "Kind")) b += 2;
                    if (Has(t, "Abrasive")) b -= 2;
                    if (Has(t, "Psychopath")) b -= 2;
                    if (Has(t, "AnnoyingVoice")) b -= 1;
                    if (Has(t, "CreepyBreathing")) b -= 1;
                    b += Degree(t, "Beauty");                // beautiful +2 ... staggeringly ugly -2
                    break;
            }
            return b;
        }

        private static bool Has(TraitSet t, string defName)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            return def != null && t.HasTrait(def);
        }

        private static int Degree(TraitSet t, string defName)
        {
            var def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            return def == null ? 0 : t.DegreeOfTrait(def);
        }
    }
}
