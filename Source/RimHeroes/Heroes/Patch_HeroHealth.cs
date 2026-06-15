using HarmonyLib;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Raid-boss capstone: a hero's body-part HP pool scales up with level via Pawn.HealthScale
    /// (which multiplies every body part's max HP uniformly), reaching x2.2 at level 20 so high-level
    /// heroes soak like a boss. Only heroes are affected; the common (non-hero) path returns instantly.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.HealthScale), MethodType.Getter)]
    public static class Patch_HeroHealth
    {
        public static void Postfix(Pawn __instance, ref float __result)
        {
            var hero = HeroUtility.GetHeroHediff(__instance);
            if (hero == null) return;
            __result *= ClassFeatures.HeroHealthFactor(hero.level);
        }
    }
}
