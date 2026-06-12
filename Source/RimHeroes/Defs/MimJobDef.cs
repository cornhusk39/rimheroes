using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A mim job caste (Porter, Sweeper, ... Wisp). Visuals follow the job, not the master's class.
    /// </summary>
    public class MimJobDef : Def
    {
        public PawnKindDef pawnKind;   // the caste PawnKindDef (race art per job); null until race exists
        public bool isCombat;          // combat mims accompany their master off-map; utility mims stay home
    }
}
