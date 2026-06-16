using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Mims are story-less animals with no SkillRecord tracker, so vanilla treats them as skill-0 for
    /// any bill that has a minimum-skill requirement (they would refuse it) and for any crafted-quality
    /// roll (they would produce awful work). These two patches give a mim its caste's
    /// CompMimWorker.fixedSkillLevel for both checks, so the Hearth (cooking) and Salve (doctor) castes -
    /// and any future skill-gated caste - can actually take skill-required work and produce sane quality.
    /// Both targets are resolved defensively (Prepare) so a signature change degrades to a no-op rather
    /// than disabling every patch.
    /// </summary>
    internal static class MimSkill
    {
        public static bool TryGet(Pawn pawn, out int level)
        {
            level = 0;
            var comp = pawn?.GetComp<CompMimWorker>();
            if (comp == null) return false;
            level = comp.Props.fixedSkillLevel;
            return true;
        }
    }

    [HarmonyPatch]
    public static class Patch_SkillRequirement_PawnSatisfies
    {
        static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(SkillRequirement), nameof(SkillRequirement.PawnSatisfies), new[] { typeof(Pawn) });

        static bool Prepare() => TargetMethod() != null;

        public static void Postfix(ref bool __result, Pawn pawn, SkillRequirement __instance)
        {
            if (__result || __instance == null) return;
            if (MimSkill.TryGet(pawn, out int level)) __result = level >= __instance.minLevel;
        }
    }

    [HarmonyPatch]
    public static class Patch_Quality_GeneratedByPawn
    {
        static MethodBase TargetMethod() =>
            AccessTools.Method(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn),
                new[] { typeof(Pawn), typeof(SkillDef), typeof(bool) });

        static bool Prepare() => TargetMethod() != null;

        public static bool Prefix(ref QualityCategory __result, Pawn pawn)
        {
            if (!MimSkill.TryGet(pawn, out int level)) return true; // not a mim: run vanilla
            __result = QualityUtility.GenerateQualityCreatedByPawn(level, false);
            return false;
        }
    }
}
