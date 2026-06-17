using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A subtle "charge" burst played at the caster when a spell goes off: a few soft motes in the
    /// class's primary colour and aura-shape, gathering inward and up like power drawn to the hand.
    /// Reuses the vestment's baseColor + auraMote so each class reads distinctly (blue arcane stars for
    /// the wizard, green leaves for the druid, purple wisps for the warlock, and so on).
    /// </summary>
    public static class SpellCastFx
    {
        public static void Play(Pawn caster)
        {
            var map = caster?.Map;
            if (map == null || !caster.Spawned) return;

            var ext = caster.health?.hediffSet?.hediffs?.OfType<Hediff_ClassVestment>().FirstOrDefault()
                ?.def.GetModExtension<VestmentExtension>();
            Color color = ext?.baseColor ?? new Color(0.6f, 0.6f, 0.85f);
            FleckDef mote = ext?.auraMote ?? RH_FleckDefOf.RH_AuraMote;
            color = Color.Lerp(color, Color.white, 0.3f); // brighten so the additive glow reads

            Vector3 center = caster.DrawPos;
            center.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            const int n = 5;
            for (int i = 0; i < n; i++)
            {
                float ang = 360f / n * i + Rand.Range(-22f, 22f);
                float rad = Rand.Range(0.4f, 0.65f);
                Vector3 loc = center + new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * rad, 0f,
                                                   Mathf.Sin(ang * Mathf.Deg2Rad) * rad + 0.2f);
                if (!loc.ShouldSpawnMotesAt(map)) continue;
                var data = FleckMaker.GetDataStatic(loc, map, mote, Rand.Range(0.45f, 0.7f));
                Color c = color;
                c.a = 0.6f; // subtle
                data.instanceColor = c;
                data.rotation = Rand.Range(0f, 360f);
                data.velocityAngle = ang + 180f + Rand.Range(-18f, 18f); // drift inward toward the caster
                data.velocitySpeed = Rand.Range(0.3f, 0.6f);
                map.flecks.CreateFleck(data);
            }
        }
    }
}
