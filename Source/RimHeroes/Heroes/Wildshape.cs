using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Druid wildshape: same-pawn hediff transformation (DESIGN.md - no pawn-swap bookkeeping).
    /// The hediff carries the form's stats, natural weapons (HediffComp_VerbGiver tools), duration
    /// (HediffComp_Disappears), and a render node drawing the beast over the hidden human frame.
    /// </summary>
    public class Hediff_Wildshape : HediffWithComps
    {
        public override bool ShouldRemove => base.ShouldRemove;

        private Graphic beastGraphic;

        /// <summary>The beast texture (directional Graphic_Multi), read from the form def's render node
        /// properties. Drawn directly by Patch_DrawBeast since runtime-added hediff render nodes do not
        /// reliably enter the draw tree.</summary>
        public Graphic BeastGraphic
        {
            get
            {
                if (beastGraphic == null)
                {
                    var rp = def.renderNodeProperties != null && def.renderNodeProperties.Count > 0
                        ? def.renderNodeProperties[0] : null;
                    if (rp != null && !rp.texPath.NullOrEmpty())
                    {
                        beastGraphic = GraphicDatabase.Get<Graphic_Multi>(rp.texPath, ShaderDatabase.Cutout,
                            rp.drawSize, rp.color ?? Color.white);
                    }
                }
                return beastGraphic;
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            // One form at a time: taking a new shape ends the previous one.
            var hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
            {
                if (hediffs[i] is Hediff_Wildshape other && other != this)
                {
                    pawn.health.RemoveHediff(other);
                }
            }
            RebuildRenderTree();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            RebuildRenderTree();
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message("RH_WildshapeReverted".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NeutralEvent);
            }
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (pawn.IsColonistPlayerControlled)
            {
                yield return new Command_Action
                {
                    defaultLabel = "RH_RevertForm".Translate(),
                    defaultDesc = "RH_RevertFormDesc".Translate(),
                    icon = RH_Tex.RevertForm,
                    action = () => pawn.health.RemoveHediff(this)
                };
            }
        }

        // A hediff added at runtime only gets its render node into the draw tree if the tree's NODE
        // STRUCTURE is rebuilt. SetAllGraphicsDirty alone just re-resolves existing graphics, so the
        // beast node (cached but never added) never draws. SetDirty forces a full structural rebuild.
        private void RebuildRenderTree()
        {
            var renderer = pawn?.Drawer?.renderer;
            if (renderer == null) return;
            renderer.renderTree?.SetDirty();
            renderer.SetAllGraphicsDirty();
        }

        public static bool IsShifted(Pawn pawn)
        {
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null)
            {
                return false;
            }
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Wildshape)
                {
                    return true;
                }
            }
            return false;
        }
    }

    /// <summary>Draws the beast form (tinted placeholder texture) in place of the hidden pawn.</summary>
    public class PawnRenderNode_Wildshape : PawnRenderNode
    {
        public PawnRenderNode_Wildshape(Pawn pawn, PawnRenderNodeProperties props, PawnRenderTree tree)
            : base(pawn, props, tree) { }

        public override Graphic GraphicFor(Pawn pawn)
        {
            if (props.texPath.NullOrEmpty())
            {
                return null;
            }
            return GraphicDatabase.Get<Graphic_Multi>(props.texPath, ShaderDatabase.Cutout,
                props.drawSize, props.color ?? Color.white);
        }
    }

    /// <summary>
    /// While wildshaped: hide the human head/hair/beard/eyes (those are separate render subtrees that
    /// do NOT contain the beast) and all worn apparel. The human BODY graphic is suppressed separately
    /// in Patch_PawnRenderNode_Body_GraphicFor by returning null from the body node, NOT via a Body
    /// skip flag - because the beast render node is parented under the Body tag, and a skip flag culls
    /// the tagged node together with its entire subtree, which would take the beast down with it.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderTree), "AdjustParms")]
    public static class Patch_PawnRenderTree_AdjustParms
    {
        private static RenderSkipFlagDef bodyFlag;
        private static bool bodyFlagResolved;
        private static RenderSkipFlagDef BodyFlag
        {
            get
            {
                if (!bodyFlagResolved)
                {
                    bodyFlag = DefDatabase<RenderSkipFlagDef>.GetNamedSilentFail("Body");
                    bodyFlagResolved = true;
                }
                return bodyFlag;
            }
        }

        public static void Postfix(PawnRenderTree __instance, ref PawnDrawParms parms)
        {
            var vestment = __instance.pawn?.health?.hediffSet?.hediffs
                ?.OfType<Hediff_ClassVestment>().FirstOrDefault();

            // Heroes loom: draw scale grows with vestment tier (1.05 at T1 up to 1.25 at T5).
            // Portraits stay unscaled so the bio card doesn't clip.
            if (vestment != null && !parms.Portrait)
            {
                float s = 1f + 0.05f * vestment.Tier;
                parms.matrix *= Matrix4x4.TRS(
                    new Vector3(0f, 0f, (s - 1f) * 0.35f), Quaternion.identity, new Vector3(s, 1f, s));
            }

            if (Hediff_Wildshape.IsShifted(__instance.pawn))
            {
                // Hide the entire human body subtree; the beast itself is drawn by Patch_DrawBeast.
                if (BodyFlag != null) parms.skipFlags |= BodyFlag;
                parms.skipFlags |= RenderSkipFlagDefOf.Head;
                parms.skipFlags |= RenderSkipFlagDefOf.Hair;
                parms.skipFlags |= RenderSkipFlagDefOf.Beard;
                parms.skipFlags |= RenderSkipFlagDefOf.Eyes;
                parms.flags &= ~(PawnRenderFlags.Clothes | PawnRenderFlags.Headgear);
                return;
            }
            // Vestment headpieces work like hats: hide hair while a HelmT<tier> texture renders
            // (beards stay - a skull cap shouldn't swallow the beard). Headband-style pieces
            // (helmKeepsHair) sit over the hair, so they don't skip it. HelmHidesHair is cached
            // because this runs during parallel pre-draw where ContentFinder is off-limits.
            if (vestment != null && vestment.HelmHidesHair)
            {
                parms.skipFlags |= RenderSkipFlagDefOf.Hair;
            }
        }
    }

    /// <summary>
    /// Draws the beast graphic directly over the (body-hidden) pawn while wildshaped. Runtime-added
    /// hediff render nodes do not reliably enter the pawn draw tree, so we draw the form ourselves
    /// in the render postfix, picking the directional texture from the pawn's facing.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_DrawBeast
    {
        public static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
        {
            var pawn = __instance.pawn;
            var hediffs = pawn?.health?.hediffSet?.hediffs;
            if (hediffs == null) return;
            Hediff_Wildshape form = null;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Wildshape w) { form = w; break; }
            }
            if (form == null) return;
            var g = form.BeastGraphic;
            if (g == null) return;
            var loc = drawLoc;
            loc.y += 0.046875f; // a couple of altitude layers up, above the hidden body
            g.Draw(loc, pawn.Rotation, pawn);
        }
    }
}
