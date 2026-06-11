using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A gestral job caste (Porter, Sweeper, ... Wisp). Visuals follow the job, not the master's class.
    /// </summary>
    public class GestralJobDef : Def
    {
        public PawnKindDef pawnKind;   // the caste PawnKindDef (race art per job); null until race exists
        public bool isCombat;          // combat gestrals accompany their master off-map; utility gestrals stay home
    }
}
