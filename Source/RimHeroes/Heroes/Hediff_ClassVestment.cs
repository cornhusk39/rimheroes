using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Per-class config for the vestment's look (placeholder: tinted vanilla apparel texture).
    /// </summary>
    public class VestmentExtension : DefModExtension
    {
        public Color baseColor = Color.gray;
    }

    /// <summary>
    /// The class vestment: persistent armor-as-hediff (DESIGN.md). Unremovable and pawn-bound by
    /// definition; severity == tier (1-4, advancing at hero levels 5/11/17); stages carry the armor
    /// stats; visuals via a render node that re-resolves on tier change. Surgery recipes
    /// (Recipe_VestmentEnhancement) add enhancement hediffs alongside it.
    /// </summary>
    public class Hediff_ClassVestment : HediffWithComps
    {
        public override bool ShouldRemove => false;

        public int Tier => Mathf.Clamp(Mathf.RoundToInt(Severity), 1, 4);

        public override string LabelInBrackets => $"tier {Tier}";

        public Color TierColor
        {
            get
            {
                var baseColor = def.GetModExtension<VestmentExtension>()?.baseColor ?? Color.gray;
                // dull at tier 1, full class color at tier 4
                return Color.Lerp(new Color(0.45f, 0.45f, 0.45f), baseColor, (Tier - 1) / 3f);
            }
        }

        public static int TierForLevel(int level) => level >= 17 ? 4 : level >= 11 ? 3 : level >= 5 ? 2 : 1;

        public void SetTierForLevel(int level)
        {
            int tier = TierForLevel(level);
            if (Tier == tier && Severity >= 1f)
            {
                return;
            }
            bool upgraded = tier > Tier && Severity >= 1f;
            Severity = tier;
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            if (upgraded && PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message("RH_VestmentUpgraded".Translate(pawn.LabelShortCap, def.label, tier),
                    pawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    /// <summary>
    /// Render node whose graphic depends on the vestment's tier (color) and the pawn's body type
    /// (apparel-style _BodyType texture suffix). Re-resolved via SetAllGraphicsDirty on tier change.
    /// </summary>
    public class PawnRenderNode_Vestment : PawnRenderNode
    {
        public PawnRenderNode_Vestment(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (Hediff_Wildshape.IsShifted(pawn))
            {
                return null; // the vestment transforms with the druid - hidden while shifted
            }
            var vestment = pawn.health?.hediffSet?.hediffs?.OfType<Hediff_ClassVestment>().FirstOrDefault();
            if (vestment == null || props.texPath.NullOrEmpty())
            {
                return null;
            }
            string path = props.texPath;
            if (pawn.story?.bodyType != null)
            {
                path += "_" + pawn.story.bodyType.defName;
            }
            return GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout, Vector2.one, vestment.TierColor);
        }
    }

    /// <summary>
    /// Vestment inlay surgery: only offered on pawns wearing a class vestment. Installing into an
    /// occupied slot replaces the old inlay (destroyed). Re-installing the identical inlay is
    /// blocked by vanilla's duplicate check.
    /// </summary>
    public class Recipe_VestmentEnhancement : Recipe_AddHediff
    {
        public override bool AvailableOnNow(Thing thing, BodyPartRecord part = null)
        {
            return thing is Pawn pawn
                   && pawn.health.hediffSet.hediffs.Any(h => h is Hediff_ClassVestment)
                   && base.AvailableOnNow(thing, part);
        }

        public override void ApplyOnPawn(Pawn pawn, BodyPartRecord part, Pawn billDoer, System.Collections.Generic.List<Thing> ingredients, Bill bill)
        {
            // Clear the slot first so base's AddHediff lands in a free slot.
            var ext = InlayUtility.ExtensionOf(recipe.addsHediff);
            if (ext != null)
            {
                var occupant = InlayUtility.InlayInSlot(pawn, ext.slot);
                if (occupant != null && occupant.def != recipe.addsHediff)
                {
                    pawn.health.RemoveHediff(occupant);
                    if (PawnUtility.ShouldSendNotificationAbout(pawn))
                    {
                        Messages.Message("RH_InlayReplaced".Translate(pawn.LabelShortCap, occupant.def.label, recipe.addsHediff.label),
                            pawn, MessageTypeDefOf.NeutralEvent);
                    }
                }
            }
            base.ApplyOnPawn(pawn, part, billDoer, ingredients, bill);
        }
    }
}
