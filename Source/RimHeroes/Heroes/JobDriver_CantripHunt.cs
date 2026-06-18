using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    /// <summary>
    /// One continuous job for a caster hero hunting marked prey: walk to spell range, cast the cantrip,
    /// hold while it recharges, then cast again until the prey drops. Because it is a single job rather
    /// than a per-cast work scan, the hunter never flickers to idle/leisure between casts; and because it
    /// drives the cantrip's own verb, it paths only to spell range and never closes to melee.
    /// </summary>
    public class JobDriver_CantripHunt : JobDriver
    {
        private const TargetIndex PreyInd = TargetIndex.A;

        private Pawn Prey => job.GetTarget(PreyInd).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed) =>
            pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed);

        public override IEnumerable<Toil> MakeNewToils()
        {
            var cantrip = HeroHunt.OffensiveCantrip(pawn);
            if (cantrip == null)
            {
                yield break;
            }
            // Drive the cantrip's verb so the goto positions at spell range, not melee range.
            job.verbToUse = cantrip.verb;

            this.FailOnDespawnedNullOrForbidden(PreyInd);
            this.FailOn(() =>
            {
                var prey = Prey;
                return prey == null || prey.Dead || prey.Downed
                       || HeroHunt.OffensiveCantrip(pawn) == null
                       || pawn.Map.designationManager.DesignationOn(prey, DesignationDefOf.Hunt) == null;
            });

            Toil goToRange = Toils_Combat.GotoCastPosition(PreyInd);
            yield return goToRange;

            // Hold position (facing the prey) until the cantrip is off cooldown.
            Toil waitReady = new Toil { defaultCompleteMode = ToilCompleteMode.Never, handlingFacing = true };
            waitReady.tickAction = () =>
            {
                var prey = Prey;
                if (prey != null)
                {
                    pawn.rotationTracker.FaceTarget(prey);
                }
                if (HeroHunt.OffensiveCantrip(pawn)?.CanCast == true)
                {
                    ReadyForNextToil();
                }
            };
            yield return waitReady;

            // Fire the cantrip at the prey (applies the spell's effect and starts its cooldown).
            Toil cast = new Toil { defaultCompleteMode = ToilCompleteMode.Instant };
            cast.initAction = () =>
            {
                var c = HeroHunt.OffensiveCantrip(pawn);
                var prey = Prey;
                if (c != null && prey != null && c.CanCast)
                {
                    pawn.rotationTracker.FaceTarget(prey);
                    c.Activate(prey, prey);
                }
            };
            yield return cast;

            yield return Toils_Jump.Jump(goToRange);
        }
    }
}
