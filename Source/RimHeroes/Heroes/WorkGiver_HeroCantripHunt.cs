using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    /// <summary>
    /// Caster heroes hunt by casting their offensive cantrip at the marked prey from range, rather than
    /// closing to melee. For each hunt-designated animal this hands back the cantrip's own cast job, which
    /// paths only to spell range and never to melee; once it resolves and the animal is still marked and
    /// alive, the work loop simply issues the next cast. Martial heroes are untouched (they melee-hunt
    /// through the vanilla work-giver).
    /// </summary>
    public class WorkGiver_HeroCantripHunt : WorkGiver_Scanner
    {
        public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Pawn);

        public override PathEndMode PathEndMode => PathEndMode.Touch;

        public override IEnumerable<Thing> PotentialWorkThingsGlobal(Pawn pawn)
        {
            foreach (var des in pawn.Map.designationManager.SpawnedDesignationsOfDef(DesignationDefOf.Hunt))
            {
                yield return des.target.Thing;
            }
        }

        public override bool ShouldSkip(Pawn pawn, bool forced = false)
        {
            return HeroHunt.OffensiveCantrip(pawn) == null
                   || !pawn.Map.designationManager.AnySpawnedDesignationOfDef(DesignationDefOf.Hunt);
        }

        public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (!(t is Pawn prey) || prey.Dead || prey.Map != pawn.Map)
            {
                return false;
            }
            if (pawn.Map.designationManager.DesignationOn(t, DesignationDefOf.Hunt) == null)
            {
                return false;
            }
            return HeroHunt.OffensiveCantrip(pawn) != null && pawn.CanReserve(t, 1, -1, null, forced);
        }

        public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
        {
            if (HeroHunt.OffensiveCantrip(pawn) == null)
            {
                return null;
            }
            // One continuous job runs the whole hunt (approach, cast, recharge, repeat) so the hunter
            // never flickers back to idle between casts.
            return JobMaker.MakeJob(RH_DefOf.RH_CantripHunt, t);
        }
    }
}
