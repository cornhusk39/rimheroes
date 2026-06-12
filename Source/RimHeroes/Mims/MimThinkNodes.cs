using Verse;
using Verse.AI;

namespace RimHeroes
{
    public static class DevotionUtility
    {
        public static Hediff_MimDevotion GetDevotion(this Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(RH_DefOf.RH_MimDevotion) as Hediff_MimDevotion;
    }

    public class ThinkNode_ConditionalDevotionState : ThinkNode_Conditional
    {
        public DevotionState state;

        public override ThinkNode DeepCopy(bool resolve = true)
        {
            var node = (ThinkNode_ConditionalDevotionState)base.DeepCopy(resolve);
            node.state = state;
            return node;
        }

        public override bool Satisfied(Pawn pawn) => pawn.GetDevotion()?.StateNow == state;
    }

    public class ThinkNode_ConditionalPanicLeaving : ThinkNode_Conditional
    {
        public override bool Satisfied(Pawn pawn) => pawn.GetDevotion()?.InLeavePhase == true;
    }

    /// <summary>Vigil: hover near the ailing master instead of working.</summary>
    public class JobGiver_VigilNearMaster : JobGiver_Wander
    {
        public JobGiver_VigilNearMaster()
        {
            wanderRadius = 4f;
            ticksBetweenWandersRange = new IntRange(80, 200);
            locomotionUrgency = LocomotionUrgency.Walk;
            maxDanger = Danger.Some;
        }

        public override IntVec3 GetWanderRoot(Pawn pawn)
        {
            var master = pawn.GetDevotion()?.master;
            if (master != null && master.Spawned && master.Map == pawn.Map)
            {
                return master.Position;
            }
            return pawn.Position;
        }
    }
}
