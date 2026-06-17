using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Heroes carry class-bound melee weapons or caster staves, so vanilla's "needs a ranged weapon"
    /// hunt gate refuses them. This re-admits heroes as hunters: a martial walks up and fights with its
    /// weapon (vanilla melee hunting), and a caster opens up with its offensive cantrip from range
    /// (see Hediff_HeroLevels.TryCantripHunt, which fires the cantrip at the prey while the hunt job runs).
    /// </summary>
    [HarmonyPatch(typeof(WorkGiver_HunterHunt), "HasHuntingWeapon")]
    public static class Patch_WorkGiver_HunterHunt_HasHuntingWeapon
    {
        public static void Postfix(Pawn p, ref bool __result)
        {
            if (__result || p == null || !HeroUtility.IsHero(p))
            {
                return;
            }
            if (HeroHunt.HasUsableMeleeWeapon(p) || HeroHunt.OffensiveCantrip(p) != null)
            {
                __result = true;
            }
        }
    }

    /// <summary>Shared helpers for hero hunting: how a martial fights prey and how a caster picks the
    /// cantrip it hunts with.</summary>
    public static class HeroHunt
    {
        public static bool HasUsableMeleeWeapon(Pawn p)
        {
            var w = p?.equipment?.Primary;
            return w != null && w.def.IsMeleeWeapon;
        }

        /// <summary>The hero's first single-target offensive cantrip (Fire Bolt, Ray of Frost, Eldritch
        /// Blast, ...), or null if it has none. Used both to admit casters as hunters and to choose what
        /// they fling at the prey.</summary>
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
