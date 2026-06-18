using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    /// <summary>
    /// Heroes carry class-bound melee weapons or caster staves, so vanilla's "needs a ranged weapon"
    /// hunt gate refuses them and the "no hunter has a valid weapon" message fires. Every hero is a
    /// valid hunter: a martial walks up and fights with its weapon (vanilla melee hunting), and a caster
    /// is routed away from melee hunting (JobOnThing nulled) over to WorkGiver_HeroCantripHunt, which
    /// has it fling its cantrip from range instead of closing in.
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_HunterHunt), "HasHuntingWeapon")]
    public static class Patch_WorkGiver_HunterHunt_HasHuntingWeapon
    {
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (!__result && p != null && HeroUtility.IsHero(p))
            {
                __result = true;
            }
        }
    }

    /// <summary>Stops a caster hero from taking the vanilla (melee) hunt job, so it hunts only through
    /// WorkGiver_HeroCantripHunt and stays at range instead of walking up to club the prey.</summary>
    [HarmonyPatch(typeof(WorkGiver_HunterHunt), "JobOnThing")]
    public static class Patch_WorkGiver_HunterHunt_JobOnThing
    {
        public static void Postfix(Pawn pawn, ref Job __result)
        {
            if (__result != null && HeroUtility.IsHero(pawn) && HeroHunt.OffensiveCantrip(pawn) != null)
            {
                __result = null;
            }
        }
    }

    /// <summary>Shared helper: the cantrip a caster hunts with.</summary>
    public static class HeroHunt
    {
        /// <summary>The hero's first single-target offensive cantrip (Fire Bolt, Ray of Frost, Produce
        /// Flame, ...), or null if it has none. Used to route casters to ranged hunting and to choose
        /// what they fling at the prey.</summary>
        public static Ability OffensiveCantrip(Pawn p)
        {
            if (p?.abilities == null)
            {
                return null;
            }
            foreach (var a in p.abilities.abilities)
            {
                if (a?.def != null && SpellUtility.IsCantrip(a.def) && a.def.hostile
                    && HeroAutocast.ClassifyIntent(a) == HeroAutocast.Intent.HostileSingle)
                {
                    return a;
                }
            }
            return null;
        }
    }
}
