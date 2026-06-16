using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A dungeon entrance the party delves through. A MapPortal that also remembers which DungeonKind
    /// it leads to; the generic GenSteps read that off PocketMapUtility.currentlyGeneratingPortal while
    /// the pocket map is being built. The incident sets the kind when it spawns the entrance.
    /// </summary>
    public class Building_DungeonEntrance : MapPortal
    {
        public DungeonKindDef kind;
        public int tier = 1;
        public float difficulty = 1f;

        // Dev-only overrides so spikes can generate a chosen kind/tier without a live portal-enter flow.
        public static DungeonKindDef DebugForcedKind;
        public static int DebugForcedTier = 1;
        public static float DebugForcedDifficulty = 1f;

        public static Building_DungeonEntrance Generating =>
            PocketMapUtility.currentlyGeneratingPortal as Building_DungeonEntrance;

        public static DungeonKindDef GeneratingKind =>
            DebugForcedKind
            ?? Generating?.kind
            ?? DefDatabase<DungeonKindDef>.GetNamedSilentFail("RH_Dungeon_Crypt");

        public override string Label => kind != null ? kind.LabelCap : base.Label;

        private Graphic cachedGraphic;

        // Per-theme entrance sprite, when the kind specifies one; otherwise the default crypt stairway.
        public override Graphic Graphic
        {
            get
            {
                if (kind == null || kind.entranceTexPath.NullOrEmpty()) return base.Graphic;
                if (cachedGraphic == null)
                    cachedGraphic = GraphicDatabase.Get<Graphic_Single>(
                        kind.entranceTexPath, ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
                return cachedGraphic;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref kind, "kind");
            Scribe_Values.Look(ref tier, "tier", 1);
            Scribe_Values.Look(ref difficulty, "difficulty", 1f);
        }
    }
}
