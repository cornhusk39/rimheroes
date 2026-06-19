using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    /// <summary>
    /// When the player orders a caster hero to attack an enemy (right-click), a spellcaster should sling
    /// a spell, not walk up and club them with a staff. If the hero has an offensive cantrip, the melee/
    /// shoot order is redirected into casting that cantrip at the target. Martial heroes (no cantrip) are
    /// untouched and attack normally.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
    public static class Patch_Pawn_JobTracker_TryTakeOrderedJob
    {
        public static bool Prefix(Pawn_JobTracker __instance, Job job, ref bool __result)
        {
            if (job == null || (job.def != JobDefOf.AttackMelee && job.def != JobDefOf.AttackStatic))
            {
                return true;
            }
            if (!(job.targetA.Thing is Pawn target) || target.Dead)
            {
                return true;
            }
            var pawn = __instance.pawn;
            if (pawn == null || !HeroUtility.IsHero(pawn))
            {
                return true;
            }
            var cantrip = HeroHunt.OffensiveCantrip(pawn);
            if (cantrip == null)
            {
                return true; // martial hero with no cantrip: attack normally (melee/shoot)
            }
            if (!cantrip.CanCast)
            {
                // Caster, but the cantrip is recharging: refuse the order rather than walk into melee.
                // Autocast will fire the cantrip at a nearby foe the moment it is ready.
                __result = false;
                return false;
            }
            cantrip.QueueCastingJob(target, target);
            __result = true;
            return false; // skip the melee/shoot order; the cantrip cast takes its place
        }
    }
}
