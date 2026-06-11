using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHeroes
{
    public class CompProperties_GestralWorker : CompProperties
    {
        // Work types this gestral caste performs (e.g. Cleaning for the Sweeper).
        public List<WorkTypeDef> workTypes = new List<WorkTypeDef>();

        public CompProperties_GestralWorker() => compClass = typeof(CompGestralWorker);
    }

    /// <summary>
    /// Gives a non-humanlike, non-mech pawn functioning vanilla work settings so JobGiver_Work
    /// can drive real WorkGivers. Spike finding: the only hard gate in the vanilla pipeline is
    /// JobGiver_Work.PawnCanUseWorkGiver's colonist/mech check (see Patch_JobGiver_Work);
    /// GetDisabledWorkTypes disables nothing for a story-less non-mech race, and
    /// Pawn_WorkSettings is null-safe on skills.
    /// </summary>
    public class CompGestralWorker : ThingComp
    {
        public CompProperties_GestralWorker Props => (CompProperties_GestralWorker)props;

        private Pawn Pawn => (Pawn)parent;

        public bool Allows(WorkTypeDef w) => Props.workTypes.Contains(w);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            InitializeWorkSettings();
        }

        private void InitializeWorkSettings()
        {
            var p = Pawn;
            if (p.workSettings == null)
            {
                p.workSettings = new Pawn_WorkSettings(p);
            }
            p.workSettings.EnableAndInitializeIfNotAlreadyInitialized();
            foreach (var w in DefDatabase<WorkTypeDef>.AllDefsListForReading)
            {
                p.workSettings.SetPriority(w, Allows(w) ? 1 : 0);
            }
        }
    }
}
