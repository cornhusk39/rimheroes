using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The vestment IS the hero's armor: pawns wearing one cannot equip torso armor layers
    /// (Middle/Shell). Skin-layer clothing (shirts) and non-torso apparel remain allowed.
    /// HasPartsToWear is the single choke point used by wear jobs, float menus, and apparel
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
            if (p.health?.hediffSet?.hediffs == null || !p.health.hediffSet.hediffs.Any(h => h is Hediff_ClassVestment))
            {
                return;
            }
            var props = apparel.apparel;
            bool coversTorso = props.bodyPartGroups?.Contains(BodyPartGroupDefOf.Torso) ?? false;
            bool armorLayer = props.layers != null
                              && (props.layers.Contains(ApparelLayerDefOf.Middle) || props.layers.Contains(ApparelLayerDefOf.Shell));
            if (coversTorso && armorLayer)
            {
                __result = false;
            }
        }
    }
}
