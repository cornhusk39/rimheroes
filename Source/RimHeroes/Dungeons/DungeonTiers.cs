using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Maps colony threat-points (RimWorld's wealth + population + time budget) to a dungeon tier and a
    /// difficulty multiplier. Tier gates which dungeons can appear (easy kinds early, the worst horrors
    /// only once you are strong); difficulty smoothly scales mob counts and loot within a tier.
    /// </summary>
    public static class DungeonTiers
    {
        public const int Delve = 1;
        public const int Dungeon = 2;
        public const int Capstone = 3;

        public static float PointsForMap(Map map)
        {
            if (map == null) return 300f;
            try { return StorytellerUtility.DefaultThreatPointsNow(map); }
            catch { return 300f; }
        }

        public static int TierForPoints(float points)
        {
            int t = points >= 1800f ? Capstone : points >= 600f ? Dungeon : Delve;
            // a little variance so it is not perfectly deterministic, kept within bounds
            if (Rand.Chance(0.18f)) t = Mathf.Clamp(t + (Rand.Bool ? 1 : -1), Delve, Capstone);
            return t;
        }

        /// <summary>~0.8x at the poorest up to ~2.4x for a rich colony.</summary>
        public static float DifficultyForPoints(float points) => Mathf.Clamp(0.8f + points / 1500f, 0.8f, 2.4f);
    }
}
