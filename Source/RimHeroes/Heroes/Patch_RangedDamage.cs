using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Ranger ranged scaling: Extra Attack (+damage) and Favored Enemy / Foe Slayer (+damage vs the
    /// chosen foe type) apply to the ranger's projectiles. Only rangers are affected.
    /// (Projectile.DamageAmount returns int.)
    /// </summary>
    [HarmonyPatch(typeof(Projectile), "DamageAmount", MethodType.Getter)]
    public static class Patch_RangedDamage
    {
        public static void Postfix(Projectile __instance, ref int __result)
        {
            if (__result <= 0 || !(__instance.Launcher is Pawn shooter)) return;
            var hero = HeroUtility.GetHeroHediff(shooter);
            if (hero == null) return;
            var target = Traverse.Create(__instance).Field("usedTarget").GetValue<LocalTargetInfo>().Thing;
            float f = ClassFeatures.RangedDamageFactor(hero, target);
            if (f != 1f) __result = Mathf.RoundToInt(__result * f);
        }
    }
}
