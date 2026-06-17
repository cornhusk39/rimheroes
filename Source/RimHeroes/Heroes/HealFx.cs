using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Healing VFX. Every healed pawn gets a green bloom at their feet (a radiating ground glow + a few
    /// motes rising up), lasting about 0.8s. Radius heals also flash a translucent range ring around the
    /// caster, and some spells add motes over the caster (e.g. Power Word Heal's music notes).
    /// </summary>
    public static class HealFx
    {
        public static readonly Color Green = new Color(0.35f, 0.95f, 0.45f);

        /// <summary>Green (or tinted) bloom on a healed pawn: radiating ground glow + upward marks.</summary>
        public static void Bloom(Pawn p, Color color)
        {
            var map = p?.Map;
            if (map == null || !p.Spawned) return;
            Vector3 loc = p.DrawPos;
            if (!loc.ShouldSpawnMotesAt(map)) return;

            var ground = FleckMaker.GetDataStatic(loc, map, RH_FleckDefOf.RH_Fleck_HealBloom, 1.3f);
            Color gc = color; gc.a = 0.8f;
            ground.instanceColor = gc;
            map.flecks.CreateFleck(ground);

            for (int i = 0; i < 3; i++)
            {
                Vector3 mloc = loc + new Vector3(Rand.Range(-0.32f, 0.32f), 0f, Rand.Range(-0.2f, 0.25f));
                var mark = FleckMaker.GetDataStatic(mloc, map, RH_FleckDefOf.RH_AuraMote, Rand.Range(0.4f, 0.6f));
                Color mc = color; mc.a = 0.75f;
                mark.instanceColor = mc;
                mark.rotation = Rand.Range(0f, 360f);
                mark.velocityAngle = Rand.Range(80f, 100f);   // rise upward (north on screen)
                mark.velocitySpeed = Rand.Range(0.6f, 1.0f);
                map.flecks.CreateFleck(mark);
            }
        }

        /// <summary>A translucent circular outline showing a radius spell's reach, centred on the heal
        /// point (the caster, for a self-cast party heal).</summary>
        public static void RangeRing(Map map, Vector3 center, float radius, Color color)
        {
            if (map == null || radius <= 0f) return;
            var ring = FleckMaker.GetDataStatic(center, map, RH_FleckDefOf.RH_Fleck_RangeRing, radius * 2f);
            Color c = color; c.a = 0.6f;
            ring.instanceColor = c;
            map.flecks.CreateFleck(ring);
        }

        /// <summary>A burst of motes over the caster (e.g. Power Word Heal's green music notes).</summary>
        public static void CasterMotes(Pawn caster, FleckDef def, Color color, int count, float scale)
        {
            var map = caster?.Map;
            if (map == null || !caster.Spawned || def == null) return;
            for (int i = 0; i < count; i++)
            {
                Vector3 loc = caster.DrawPos + new Vector3(Rand.Range(-0.55f, 0.55f), 0f, Rand.Range(0.35f, 0.95f));
                if (!loc.ShouldSpawnMotesAt(map)) continue;
                var d = FleckMaker.GetDataStatic(loc, map, def, scale * Rand.Range(0.85f, 1.15f));
                Color c = color; c.a = 0.85f;
                d.instanceColor = c;
                d.rotation = Rand.Range(-20f, 20f);
                d.velocityAngle = Rand.Range(82f, 98f);
                d.velocitySpeed = Rand.Range(0.35f, 0.6f);
                map.flecks.CreateFleck(d);
            }
        }
    }
}
