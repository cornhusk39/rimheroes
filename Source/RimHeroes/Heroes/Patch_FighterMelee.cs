using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Fighter melee modifiers that have no pawn stat to ride: Strength (+damage), Extra Attack
    /// (+damage), Action Surge (+damage), and Cunning (+armor penetration) scale the per-hit damage;
    /// Quickness (and Action Surge) cut the melee cooldown. Only fighters are affected.
    /// </summary>
    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply", new[] { typeof(LocalTargetInfo) })]
    public static class Patch_FighterMelee_Damage
    {
        public static IEnumerable<DamageInfo> Postfix(IEnumerable<DamageInfo> __result, Verb_MeleeAttackDamage __instance)
        {
            var hero = HeroUtility.GetHeroHediff(__instance.CasterPawn);
            float dmg = ClassFeatures.MeleeDamageFactor(hero);
            float pen = ClassFeatures.MeleeArmorPenOffset(hero);
            bool mod = hero != null && (dmg != 1f || pen != 0f);
            foreach (var di in __result)
            {
                if (!mod)
                {
                    yield return di;
                    continue;
                }
                yield return new DamageInfo(di.Def, di.Amount * dmg, di.ArmorPenetrationInt + pen,
                    di.Angle, di.Instigator, di.HitPart, di.Weapon, di.Category, di.IntendedTarget);
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedCooldown), new[] { typeof(Verb), typeof(Pawn) })]
    public static class Patch_FighterMelee_Cooldown
    {
        public static void Postfix(ref float __result, Pawn attacker)
        {
            float f = ClassFeatures.MeleeCooldownFactor(HeroUtility.GetHeroHediff(attacker));
            if (f != 1f) __result *= f;
        }
    }
}
