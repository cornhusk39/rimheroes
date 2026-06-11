using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Vanilla blocks JobGiver_Work for anything that isn't a colonist, colony mech, or colony
    /// subhuman. Gestrals are none of those (IsColonyMech requires Biotech + mechanoid flesh).
    /// This postfix re-admits gestral pawns for their allowed work types, re-applying the gates
    /// the early-out skipped (work tags, ShouldSkip, required capacities).
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_Work), "PawnCanUseWorkGiver")]
    public static class Patch_JobGiver_Work_PawnCanUseWorkGiver
    {
        public static void Postfix(Pawn pawn, WorkGiver giver, ref bool __result)
        {
            if (__result)
            {
                return;
            }
            var comp = pawn.TryGetComp<CompGestralWorker>();
            if (comp == null)
            {
                return;
            }
            if (giver.def.workType == null || !comp.Allows(giver.def.workType))
            {
                return;
            }
            if (pawn.WorkTagIsDisabled(giver.def.workTags))
            {
                return;
            }
            if (giver.ShouldSkip(pawn))
            {
                return;
            }
            if (giver.MissingRequiredCapacity(pawn) != null)
            {
                return;
            }
            __result = true;
        }
    }
}
