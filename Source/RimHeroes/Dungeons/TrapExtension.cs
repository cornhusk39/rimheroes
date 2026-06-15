using Verse;

namespace RimHeroes
{
    /// <summary>Per-trap tuning for Building_DungeonTrap: the check DCs and what a failed save does.</summary>
    public class TrapExtension : DefModExtension
    {
        public int spotDC = 13;       // Perception (sight+intellect) to notice a hidden trap
        public int saveDC = 13;       // Dodge (Moving) the stepping pawn rolls to avoid harm
        public int disarmDC = 14;     // Perception to neutralize a revealed trap by hand
        public float spotRadius = 4.9f;

        public DamageDef damage;                          // resolved from <damage>Cut</damage>
        public IntRange damageAmount = new IntRange(14, 24);
    }
}
