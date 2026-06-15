using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A d20 ability/skill check. The modifier is derived from an optional RimWorld skill (5e style:
    /// roughly floor((level - 8) / 2)) plus a flat bonus; the roll is d20 + modifier vs DC. Outcome
    /// text is optional flavor shown in the roll dialog. Reused by quests, dungeon traps/locks, and
    /// social encounters via D20.Resolve.
    /// </summary>
    public class CheckDef : Def
    {
        public SkillDef skill;          // optional: modifier scales off this skill
        public int flatModifier = 0;    // optional flat bonus on top of the skill modifier
        public int dc = 10;

        // optional per-outcome flavor (falls back to the generic title when null)
        public string critSuccessText;
        public string successText;
        public string failureText;
        public string critFailureText;
    }
}
