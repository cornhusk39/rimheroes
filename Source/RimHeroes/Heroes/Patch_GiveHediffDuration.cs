using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Vanilla CompAbilityEffect_GiveHediff applies a hediff with a zero "disappears" duration (it carries
    /// no ability-level lifespan), so any timed buff cast this way (Haste, Mirror Image, Mage Armor, ...)
    /// would expire on the very next tick. This restores the buff's intended duration from its own def
    /// right after the ability applies it.
    /// </summary>
    [HarmonyPatch(typeof(CompAbilityEffect_GiveHediff), nameof(CompAbilityEffect_GiveHediff.Apply),
        new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
    public static class Patch_CompAbilityEffect_GiveHediff_Apply
    {
        public static void Postfix(CompAbilityEffect_GiveHediff __instance, LocalTargetInfo target)
        {
            var props = __instance.props as CompProperties_AbilityGiveHediff;
            if (props?.hediffDef == null)
            {
                return;
            }
            Pawn pawn = props.onlyApplyToSelf ? __instance.parent.pawn : (target.Pawn ?? __instance.parent.pawn);
            var hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(props.hediffDef);
            var disappears = hediff?.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && disappears.props is HediffCompProperties_Disappears dp
                && disappears.ticksToDisappear <= 0 && dp.disappearsAfterTicks.max > 0)
            {
                disappears.ticksToDisappear = dp.disappearsAfterTicks.RandomInRange;
            }
        }
    }
}
