using HarmonyLib;
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
        public Building sourceEntrance;   // the colony-map entrance to seal when this boss dies

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scale, "scale", 1.35f);
            Scribe_Values.Look(ref aura, "aura", Color.clear);
            Scribe_References.Look(ref sourceEntrance, "sourceEntrance");
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

    /// <summary>When a dungeon boss dies, seal its source entrance so the colony-side mob trickle stops
    /// (the dungeon has been beaten). Read in a prefix so the boss hediff is still present.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_DungeonBossDeath_Seal
    {
        public static void Prefix(Pawn __instance)
        {
            var boss = Hediff_DungeonBoss.On(__instance);
            if (boss?.sourceEntrance is Building_DungeonEntrance e && !e.Destroyed) e.Seal();
        }
    }
}
