using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    [DefOf]
    public static class RH_FleckDefOf
    {
        public static FleckDef RH_AuraMote;
        public static FleckDef RH_Fleck_WildshapeGas;
        public static FleckDef RH_Mote_Spark;
        static RH_FleckDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(RH_FleckDefOf));
    }

    /// <summary>
    /// Tier-5 capstone aura: a soft, slowly-pulsing additive glow drawn behind the hero each frame,
    /// plus a trickle of drifting motes (thrown from the vestment's Tick). Both are tinted by the
    /// class's VestmentExtension.auraColor; classes that set no auraColor get nothing.
    /// </summary>
    public static class AuraFx
    {
        private static Material glowMat;
        private static MaterialPropertyBlock mpb;

        public static void DrawGlow(Pawn pawn, Vector3 drawLoc, Color baseColor, float size = 2.1f)
        {
            if (glowMat == null)
            {
                glowMat = MaterialPool.MatFrom("Things/RimHeroes/Vestments/Effects/AuraGlow", ShaderDatabase.MoteGlow);
                mpb = new MaterialPropertyBlock();
            }
            // slow "breathing" pulse (~3.3s cycle), kept gentle so it reads as alive, not flashing
            float pulse = 0.78f + 0.22f * Mathf.Sin(Time.realtimeSinceStartup * 1.9f);
            Color c = baseColor;
            c.a *= pulse;
            mpb.SetColor(ShaderPropertyIDs.Color, c);

            Vector3 pos = drawLoc;
            pos.y -= 0.06f; // tuck behind the pawn so the bright core is occluded, leaving a halo
            var matrix = Matrix4x4.TRS(pos, Quaternion.identity, new Vector3(size, 1f, size));
            Graphics.DrawMesh(MeshPool.plane10, matrix, glowMat, 0, null, 0, mpb);
        }

        public static void ThrowMote(Pawn pawn, Color color, FleckDef fleckDef = null)
        {
            var map = pawn.Map;
            if (map == null)
            {
                return;
            }
            Vector3 loc = pawn.DrawPos + new Vector3(Rand.Range(-0.35f, 0.35f), 0f, Rand.Range(-0.25f, 0.5f));
            if (!loc.ShouldSpawnMotesAt(map))
            {
                return;
            }
            var data = FleckMaker.GetDataStatic(loc, map, fleckDef ?? RH_FleckDefOf.RH_AuraMote, Rand.Range(0.9f, 1.2f));
            Color c = color;
            c.a = 0.85f;
            data.instanceColor = c;
            data.rotation = Rand.Range(0f, 360f);
            data.velocityAngle = Rand.Range(70f, 110f); // drift upward (north on screen)
            data.velocitySpeed = Rand.Range(0.30f, 0.65f);
            map.flecks.CreateFleck(data);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_PawnRenderer_RenderPawnAt_Aura
    {
        public static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
        {
            var pawn = __instance?.pawn;
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }

            // Dungeon bosses get their kind's coloured menace glow (drawn even in beast form).
            var boss = Hediff_DungeonBoss.On(pawn);
            if (boss != null && boss.aura.a > 0f)
            {
                AuraFx.DrawGlow(pawn, drawLoc, boss.aura, 2.7f);
            }

            if (Hediff_Wildshape.IsShifted(pawn)) return;
            var vestment = pawn.health?.hediffSet?.hediffs?.OfType<Hediff_ClassVestment>().FirstOrDefault();
            if (vestment == null || vestment.Tier < 5)
            {
                return;
            }
            var color = vestment.def.GetModExtension<VestmentExtension>()?.auraColor ?? Color.clear;
            if (color.a > 0f)
            {
                AuraFx.DrawGlow(pawn, drawLoc, color);
            }
        }
    }
}
