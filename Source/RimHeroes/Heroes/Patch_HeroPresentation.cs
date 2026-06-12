using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The Hero trait reads in bright gold wherever trait labels appear (bio card, tooltips).
    /// Rich-text color tags survive RimWorld's label pipeline.
    /// </summary>
    [HarmonyPatch(typeof(Trait), nameof(Trait.LabelCap), MethodType.Getter)]
    public static class Patch_HeroTraitGold
    {
        private static readonly Color Gold = new Color(1f, 0.82f, 0.15f);

        public static void Postfix(Trait __instance, ref string __result)
        {
            if (__instance.def == RH_DefOf.RH_Hero)
            {
                __result = __result.Colorize(Gold);
            }
        }
    }
}
