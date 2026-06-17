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
        private Graphic overlayGraphic;

        // Light purple wash drawn over the beast so a druid's wildshape reads as conjured, not a wild animal.
        private static readonly Color OverlayTint = new Color(0.62f, 0.40f, 0.95f, 0.28f);

        private PawnRenderNodeProperties FormRenderProps =>
            def.renderNodeProperties != null && def.renderNodeProperties.Count > 0 ? def.renderNodeProperties[0] : null;

        /// <summary>The beast texture (directional Graphic_Multi), read from the form def's render node
        /// properties. Drawn directly by Patch_DrawBeast since runtime-added hediff render nodes do not
        /// reliably enter the draw tree.</summary>
        public Graphic BeastGraphic
        {
            get
            {
                if (beastGraphic == null)
                {
                    var rp = FormRenderProps;
                    if (rp != null && !rp.texPath.NullOrEmpty())
                    {
                        beastGraphic = GraphicDatabase.Get<Graphic_Multi>(rp.texPath, ShaderDatabase.Cutout,
                            rp.drawSize, rp.color ?? Color.white);
                    }
                }
                return beastGraphic;
            }
        }

        /// <summary>A translucent purple silhouette of the form, drawn on top of the beast as the druid-magic
        /// overlay. Same texture as the beast but on the Transparent shader so it tints rather than glows.</summary>
        public Graphic OverlayGraphic
        {
            get
            {
                if (overlayGraphic == null)
                {
                    var rp = FormRenderProps;
                    if (rp != null && !rp.texPath.NullOrEmpty())
                    {
                        overlayGraphic = GraphicDatabase.Get<Graphic_Multi>(rp.texPath, ShaderDatabase.Transparent,
                            rp.drawSize, OverlayTint);
                    }
                }
                return overlayGraphic;
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
            WildshapeFx.PlayShift(pawn);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            WildshapeFx.PlayShift(pawn); // a puff on the way out as well as in
            RebuildRenderTree();
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message("RH_WildshapeReverted".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NeutralEvent);
            }
        }

        // 5e rule: a wildshaped druid that is knocked out reverts to their own form rather than fighting
        // on as an incapacitated beast (a lethal blow is handled separately in Patch_Pawn_Kill_Wildshape).
        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn != null && pawn.Spawned && !pawn.Dead && pawn.Downed)
            {
                pawn.health.RemoveHediff(this);
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

            // Dungeon bosses loom over their lesser kin (a Menagerie monster kept at full ability, just
            // drawn larger). Skips portraits so the info card stays tidy.
            var bossH = Hediff_DungeonBoss.On(__instance.pawn);
            if (bossH != null && bossH.scale > 1.001f && !parms.Portrait)
            {
                float s = bossH.scale;
                parms.matrix *= Matrix4x4.TRS(
                    new Vector3(0f, 0f, (s - 1f) * 0.45f), Quaternion.identity, new Vector3(s, 1f, s));
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

            // light purple druid-magic wash, drawn one sub-layer above the beast
            var ov = form.OverlayGraphic;
            if (ov != null)
            {
                var oloc = loc;
                oloc.y += 0.0234375f;
                ov.Draw(oloc, pawn.Rotation, pawn);
            }
        }
    }

    /// <summary>
    /// 5e rule: when a wildshaped druid would be killed, they instead revert to their own form and
    /// survive the blow badly hurt (excess damage carries over). A beast form never dies or leaves a
    /// beast corpse. If the druid is unsalvageable even after reverting, they die as themselves.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_Wildshape
    {
        public static bool Prefix(Pawn __instance)
        {
            if (__instance?.health?.hediffSet == null) return true;
            Hediff_Wildshape form = null;
            var hediffs = __instance.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (hediffs[i] is Hediff_Wildshape w) { form = w; break; }
            }
            if (form == null) return true;

            __instance.health.RemoveHediff(form); // revert to the druid's own form

            // The beast form soaked most of the blow: pull the worst injuries back so the druid lives,
            // left hurt (and likely downed). A still-fatal state (e.g. a destroyed vital part) falls through
            // to a normal death as the druid - which leaves a human corpse, never a beast.
            var hs = __instance.health.hediffSet;
            foreach (var inj in hs.hediffs.OfType<Hediff_Injury>().ToList())
            {
                inj.Severity = Mathf.Max(0f, inj.Severity * 0.4f);
                if (inj.Severity <= 0.01f) __instance.health.RemoveHediff(inj);
            }
            hs.DirtyCache();
            if (__instance.health.ShouldBeDead()) return true;

            if (PawnUtility.ShouldSendNotificationAbout(__instance))
            {
                Messages.Message("RH_WildshapeRevertStruck".Translate(__instance.LabelShortCap),
                    __instance, MessageTypeDefOf.NegativeHealthEvent);
            }
            return false; // skip the kill: the druid reverted and survived
        }
    }

    /// <summary>One-shot transform VFX: a short burst of soft purple gas and bright sparkles at the pawn
    /// the moment a wildshape form takes hold.</summary>
    public static class WildshapeFx
    {
        public static void PlayShift(Pawn pawn)
        {
            var map = pawn?.Map;
            if (map == null) return;
            Vector3 center = pawn.DrawPos;

            for (int i = 0; i < 9; i++)
            {
                Vector3 loc = center + new Vector3(Rand.Range(-0.45f, 0.45f), 0f, Rand.Range(-0.35f, 0.55f));
                if (!loc.ShouldSpawnMotesAt(map)) continue;
                var d = FleckMaker.GetDataStatic(loc, map, RH_FleckDefOf.RH_Fleck_WildshapeGas, Rand.Range(1.1f, 1.8f));
                Color c = new Color(0.60f, 0.34f, 0.95f, 0.70f);
                c.r += Rand.Range(-0.06f, 0.06f);
                c.b += Rand.Range(-0.05f, 0.05f);
                d.instanceColor = c;
                d.rotation = Rand.Range(0f, 360f);
                d.rotationRate = Rand.Range(-40f, 40f);
                d.velocityAngle = Rand.Range(0f, 360f);
                d.velocitySpeed = Rand.Range(0.2f, 0.55f);
                map.flecks.CreateFleck(d);
            }

            for (int i = 0; i < 8; i++)
            {
                Vector3 loc = center + new Vector3(Rand.Range(-0.55f, 0.55f), 0f, Rand.Range(-0.45f, 0.65f));
                if (!loc.ShouldSpawnMotesAt(map)) continue;
                var d = FleckMaker.GetDataStatic(loc, map, RH_FleckDefOf.RH_Mote_Spark, Rand.Range(0.45f, 0.9f));
                Color c = new Color(0.86f, 0.72f, 1f, 0.9f);
                d.instanceColor = c;
                d.rotation = Rand.Range(0f, 360f);
                d.velocityAngle = Rand.Range(0f, 360f);
                d.velocitySpeed = Rand.Range(0.5f, 1.2f);
                map.flecks.CreateFleck(d);
            }
        }
    }
}
