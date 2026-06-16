using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHeroes
{
    public class CompProperties_MimWorker : CompProperties
    {
        // Work types this mim caste performs (e.g. Cleaning for the Sweeper).
        public List<WorkTypeDef> workTypes = new List<WorkTypeDef>();

        // Mims have no skills tracker, so any skill-gated bill (a minimum-skill recipe) or quality roll
        // would treat them as unskilled. This is the skill level a mim "has" for those checks; it makes
        // the Hearth (cooking) and Salve (doctor) castes competent at skill-required work. See Patch_MimSkills.
        public int fixedSkillLevel = 8;

        public CompProperties_MimWorker() => compClass = typeof(CompMimWorker);
    }

    /// <summary>
    /// Gives a non-humanlike, non-mech pawn functioning vanilla work settings so JobGiver_Work
    /// can drive real WorkGivers. Spike finding: the only hard gate in the vanilla pipeline is
    /// JobGiver_Work.PawnCanUseWorkGiver's colonist/mech check (see Patch_JobGiver_Work);
    /// GetDisabledWorkTypes disables nothing for a story-less non-mech race, and
    /// Pawn_WorkSettings is null-safe on skills.
    /// </summary>
    public class CompMimWorker : ThingComp
    {
        public CompProperties_MimWorker Props => (CompProperties_MimWorker)props;

        private Pawn Pawn => (Pawn)parent;

        public bool Allows(WorkTypeDef w) => Props.workTypes.Contains(w);

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            InitializeWorkSettings();
            MimNames.EnsureNamed(Pawn);
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
