using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    public enum DevotionState
    {
        Content,  // master is well: mim works at its best
        Worried,  // master is sick/injured/downed: vigil instead of work
        Bereft    // master is dead: panic, then leave the map
    }

    /// <summary>
    /// The bond on a mim's side: tracks its master, mirrors the master's condition as
    /// severity (stages carry the work/move factors), and drives the panic-then-leave flow
    /// when the master dies.
    /// </summary>
    public class Hediff_MimDevotion : HediffWithComps
    {
        public Pawn master;
        public int masterDeadTick = -1;
        public int leaveAtTick = -1;

        public const int PanicDurationTicks = 15000; // ~6 in-game hours

        public override string LabelInBrackets => master != null ? master.LabelShort : base.LabelInBrackets;

        public override bool ShouldRemove => false;

        public DevotionState StateNow
        {
            get
            {
                if (master == null || master.Destroyed || master.Dead)
                {
                    return DevotionState.Bereft;
                }
                // Only keep vigil when the master is genuinely down or needs bed rest. A minor untended
                // scratch (common after any skirmish) should not stop the mim from working.
                if (master.Downed || HealthAIUtility.ShouldSeekMedicalRest(master))
                {
                    return DevotionState.Worried;
                }
                return DevotionState.Content;
            }
        }

        public bool InLeavePhase => leaveAtTick >= 0 && Find.TickManager.TicksGame >= leaveAtTick;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick(150, delta))
            {
                Sync();
            }
        }

        private void Sync()
        {
            var state = StateNow;
            if (state == DevotionState.Bereft && masterDeadTick < 0)
            {
                masterDeadTick = Find.TickManager.TicksGame;
                leaveAtTick = masterDeadTick + PanicDurationTicks;
                if (pawn.Spawned)
                {
                    // Stop whatever the mim is doing (even sleeping) so the think tree re-evaluates into
                    // the bereft panic path immediately, rather than finishing its current job first.
                    pawn.jobs?.EndCurrentJob(JobCondition.InterruptForced);
                    if (PawnUtility.ShouldSendNotificationAbout(pawn))
                    {
                        Messages.Message("RH_MimPanic".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent);
                    }
                }
            }
            else if (state != DevotionState.Bereft)
            {
                masterDeadTick = -1; // master came back (resurrection while mim still on map)
                leaveAtTick = -1;
            }
            float target = state == DevotionState.Content ? 0.1f : state == DevotionState.Worried ? 0.5f : 0.9f;
            if (!Mathf.Approximately(Severity, target))
            {
                Severity = target;
            }
        }

        public void DebugForceLeavePhase()
        {
            if (masterDeadTick < 0)
            {
                masterDeadTick = Find.TickManager.TicksGame;
            }
            leaveAtTick = Find.TickManager.TicksGame;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref master, "master");
            Scribe_Values.Look(ref masterDeadTick, "masterDeadTick", -1);
            Scribe_Values.Look(ref leaveAtTick, "leaveAtTick", -1);
        }
    }
}
