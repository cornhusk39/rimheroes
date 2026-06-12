using System.Collections.Generic;
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
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
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
    /// While wildshaped: hide the human body/head/hair/eyes and all worn apparel. One postfix on
    /// the render tree's parms assembly - the wildshape render node itself carries no skip flag,
    /// so the beast still draws.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderTree), "AdjustParms")]
    public static class Patch_PawnRenderTree_AdjustParms
    {
        private static RenderSkipFlagDef bodyFlag;
        private static RenderSkipFlagDef BodyFlag => bodyFlag ?? (bodyFlag = DefDatabase<RenderSkipFlagDef>.GetNamed("Body"));

        public static void Postfix(PawnRenderTree __instance, ref PawnDrawParms parms)
        {
            if (!Hediff_Wildshape.IsShifted(__instance.pawn))
            {
                return;
            }
            parms.skipFlags |= BodyFlag;
            parms.skipFlags |= RenderSkipFlagDefOf.Head;
            parms.skipFlags |= RenderSkipFlagDefOf.Hair;
            parms.skipFlags |= RenderSkipFlagDefOf.Beard;
            parms.skipFlags |= RenderSkipFlagDefOf.Eyes;
            parms.flags &= ~(PawnRenderFlags.Clothes | PawnRenderFlags.Headgear);
        }
    }
}
