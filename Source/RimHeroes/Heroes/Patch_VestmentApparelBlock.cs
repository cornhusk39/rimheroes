using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The vestment IS the hero's whole outfit: a pawn wearing one cannot equip any apparel at all
    /// (its armor and temperature comfort come entirely from the vestment, so nothing layers over or
    /// under it). HasPartsToWear is the single choke point used by wear jobs, float menus, and apparel
    /// generation alike.
    /// </summary>
    [HarmonyPatch(typeof(ApparelUtility), nameof(ApparelUtility.HasPartsToWear))]
    public static class Patch_ApparelUtility_HasPartsToWear
    {
        public static void Postfix(Pawn p, ThingDef apparel, ref bool __result)
        {
            if (!__result || apparel?.apparel == null)
            {
                return;
            }
            if (p.health?.hediffSet?.hediffs != null && p.health.hediffSet.hediffs.Any(h => h is Hediff_ClassVestment))
            {
                __result = false;
            }
        }
    }

    /// <summary>A hero wears no apparel by design (the vestment is their garment), so they must not feel
    /// "buck naked" about it. The vestment counts as being clothed.</summary>
    [HarmonyPatch(typeof(ThoughtWorker_PsychologicallyNude), "CurrentStateInternal")]
    public static class Patch_ThoughtWorker_Naked
    {
        public static void Postfix(Pawn p, ref ThoughtState __result)
        {
            if (__result.Active && p?.health?.hediffSet?.hediffs != null
                && p.health.hediffSet.hediffs.Any(h => h is Hediff_ClassVestment))
            {
                __result = ThoughtState.Inactive;
            }
        }
    }
}
