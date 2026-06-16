using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The dungeon-boss buff. Beyond the stat boosts in its def, this instance carries the boss's
    /// visual identity: a render scale (so it looms larger than its lesser kin) and an aura colour
    /// (a magical glow drawn behind it). Both are stamped on at spawn from the DungeonKind's boss spec,
    /// and read by the render patches. We keep the boss as its real (Menagerie) pawn so it keeps its
    /// native abilities; the scale + aura are pure presentation laid over the top.
    /// </summary>
    public class Hediff_DungeonBoss : HediffWithComps
    {
        public float scale = 1.35f;
        public Color aura = Color.clear;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scale, "scale", 1.35f);
            Scribe_Values.Look(ref aura, "aura", Color.clear);
        }

        public static Hediff_DungeonBoss On(Pawn pawn)
        {
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return null;
            for (int i = 0; i < hediffs.Count; i++)
                if (hediffs[i] is Hediff_DungeonBoss b) return b;
            return null;
        }
    }
}
