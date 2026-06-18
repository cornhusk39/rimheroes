using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A hero of any class fights by destiny (swinging steel or hurling spells), so they are never
    /// "incapable of violence" even if their origin colonist was a pacifist. This clears only the Violent
    /// work tag for heroes; any other incapabilities, and all non-heroes, are left untouched. Without it,
    /// a pacifist-turned-hero has every hostile ability greyed out and cannot hunt or fight.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.WorkTagIsDisabled))]
    public static class Patch_Pawn_WorkTagIsDisabled_HeroViolence
    {
        public static void Postfix(Pawn __instance, WorkTags w, ref bool __result)
        {
            if (!__result || (w & WorkTags.Violent) == 0 || !HeroUtility.IsHero(__instance))
            {
                return;
            }
            // Re-evaluate without the Violent bit: the hero is disabled only if some OTHER queried tag is.
            WorkTags rest = w & ~WorkTags.Violent;
            __result = rest != WorkTags.None && __instance.WorkTagIsDisabled(rest);
        }
    }
}
