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

        // Dev-only override so spikes can generate a chosen kind without a live portal-enter flow.
        public static DungeonKindDef DebugForcedKind;

        public static DungeonKindDef GeneratingKind =>
            DebugForcedKind
            ?? (PocketMapUtility.currentlyGeneratingPortal as Building_DungeonEntrance)?.kind
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
        }
    }
}
