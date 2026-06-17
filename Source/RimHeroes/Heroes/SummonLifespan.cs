using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Gives a summoned pawn a fixed lifespan: counts down and, on expiry, dissolves the pawn in a
    /// soft puff. The duration is set by the summoning ability (see CompAbilityEffect_Summon.durationTicks),
    /// so the same hediff serves any timed summon. Carries a remaining-time label.
    /// </summary>
    public class HediffCompProperties_SummonLifespan : HediffCompProperties
    {
        public HediffCompProperties_SummonLifespan() => compClass = typeof(HediffComp_SummonLifespan);
    }

    public class HediffComp_SummonLifespan : HediffComp
    {
        public int ticksLeft = -1;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksLeft, "ticksLeft", -1);
        }

        public override string CompLabelInBracketsExtra => ticksLeft > 0 ? ticksLeft.ToStringTicksToPeriod() : null;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (ticksLeft < 0) return;
            if (--ticksLeft > 0) return;

            var pawn = Pawn;
            if (pawn != null && pawn.Spawned)
            {
                Map map = pawn.Map;
                Vector3 at = pawn.DrawPos;
                for (int i = 0; i < 8; i++)
                {
                    FleckMaker.ThrowDustPuffThick(at + new Vector3(Rand.Range(-0.3f, 0.3f), 0f, Rand.Range(-0.3f, 0.3f)),
                        map, Rand.Range(1f, 1.7f), new Color(1f, 0.95f, 0.6f, 0.7f));
                }
                pawn.Destroy();
            }
        }
    }
}
