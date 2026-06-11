namespace RimHeroes
{
    /// <summary>
    /// Central tuning constants. All placeholder values pending playtest (DESIGN.md: numbers are
    /// playtest-tunable; XP pacing target is ~L5 by end of year one, L20 over a long colony arc).
    /// </summary>
    public static class RH_Tuning
    {
        // XP economy: everything trickles, combat pays (DESIGN.md XP economy).
        public const float XPPerSkillXP = 0.01f;        // work: rides skill learning
        public const float XPPerKillCombatPower = 1f;   // hostile kill: victim combatPower x this
        public const float WildKillXPFactor = 0.25f;    // hunting pays less than battle
        public const float PassiveXPPerHour = 0.5f;     // surviving/existing
        public const int PassiveXPIntervalTicks = 2500; // one in-game hour
    }
}
