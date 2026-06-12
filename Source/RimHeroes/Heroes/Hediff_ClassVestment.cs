using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Per-class config for the vestment's look. artDir points at per-tier textures under
    /// Things/RimHeroes/Vestments/&lt;artDir&gt;/ (T1..T5 + Helm); classes without art yet
    /// fall back to the tinted vanilla apparel placeholder.
    /// </summary>
    public class VestmentExtension : DefModExtension
    {
        public Color baseColor = Color.gray;
        public string artDir;
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

        public int Tier => Mathf.Clamp(Mathf.RoundToInt(Severity), 1, 5);

        public override string LabelInBrackets => $"tier {Tier}";

        public Color TierColor
        {
            get
            {
                var baseColor = def.GetModExtension<VestmentExtension>()?.baseColor ?? Color.gray;
                // dull at tier 1, full class color at tier 5
                return Color.Lerp(new Color(0.45f, 0.45f, 0.45f), baseColor, (Tier - 1) / 4f);
            }
        }

        // Five tiers: L1-4, 5-9, 10-14, 15-19, 20 - a new vestment style every fifth level.
        public static int TierForLevel(int level) => level >= 20 ? 5 : level >= 15 ? 4 : level >= 10 ? 3 : level >= 5 ? 2 : 1;

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
            if (vestment == null)
            {
                return null;
            }
            string artPath = VestmentArt.TierBodyPath(vestment, pawn);
            if (artPath != null)
            {
                return GraphicDatabase.Get<Graphic_Multi>(artPath, ShaderDatabase.Cutout, Vector2.one, Color.white);
            }
            if (props.texPath.NullOrEmpty())
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
    /// Tier-texture path resolution for vestments with real art. Layout:
    /// Things/RimHeroes/Vestments/&lt;artDir&gt;/T&lt;tier&gt;_&lt;BodyType&gt;_&lt;rot&gt;.png,
    /// falling back to the Male set when the pawn's body type has no art yet.
    /// </summary>
    public static class VestmentArt
    {
        public static string TierBodyPath(Hediff_ClassVestment vestment, Pawn pawn)
        {
            string dir = vestment.def.GetModExtension<VestmentExtension>()?.artDir;
            if (dir.NullOrEmpty())
            {
                return null;
            }
            string root = $"Things/RimHeroes/Vestments/{dir}/T{vestment.Tier}";
            string body = pawn.story?.bodyType?.defName ?? "Male";
            if (ContentFinder<Texture2D>.Get($"{root}_{body}_south", false) != null)
            {
                return $"{root}_{body}";
            }
            if (ContentFinder<Texture2D>.Get($"{root}_Male_south", false) != null)
            {
                return $"{root}_Male";
            }
            return null;
        }

        public static string HelmPath(Hediff_ClassVestment vestment)
        {
            string dir = vestment.def.GetModExtension<VestmentExtension>()?.artDir;
            if (dir.NullOrEmpty())
            {
                return null;
            }
            // Per-tier headpieces; tiers without a HelmT<n> texture (e.g. T1) draw nothing.
            string root = $"Things/RimHeroes/Vestments/{dir}/HelmT{vestment.Tier}";
            return ContentFinder<Texture2D>.Get($"{root}_south", false) != null ? root : null;
        }
    }

    /// <summary>
    /// Tiered vestment headpiece (e.g. the barbarian's skull cap through horned war-helm).
    /// Head-attached node; renders whatever HelmT&lt;tier&gt; texture the class provides.
    /// </summary>
    public class PawnRenderNode_VestmentHelm : PawnRenderNode
    {
        public PawnRenderNode_VestmentHelm(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (Hediff_Wildshape.IsShifted(pawn))
            {
                return null;
            }
            var vestment = pawn.health?.hediffSet?.hediffs?.OfType<Hediff_ClassVestment>().FirstOrDefault();
            if (vestment == null)
            {
                return null;
            }
            string path = VestmentArt.HelmPath(vestment);
            return path == null ? null
                : GraphicDatabase.Get<Graphic_Multi>(path, ShaderDatabase.Cutout, Vector2.one, Color.white);
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
