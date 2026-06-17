using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>On-target spell VFX: a quick radiant flash over a struck pawn, and a brief downward
    /// column of light that descends onto it (Divine Smite, Sacred Flame).</summary>
    public static class SpellFx
    {
        /// <summary>A quick fading radiant glow over the target pawn.</summary>
        public static void RadiantFlash(Pawn p, Color color)
        {
            var map = p?.Map;
            if (map == null || !p.Spawned) return;
            Vector3 loc = p.DrawPos;
            loc.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (!loc.ShouldSpawnMotesAt(map)) return;
            var d = FleckMaker.GetDataStatic(loc, map, RH_FleckDefOf.RH_Fleck_RadiantFlash, 1.5f);
            Color c = color; c.a = 0.65f;
            d.instanceColor = c;
            map.flecks.CreateFleck(d);
        }

        /// <summary>A brief radiant column descending onto the target: a downward Ray beam (so it swells
        /// from nothing then shrinks away), one pawn-width wide, capped at 30% opacity.</summary>
        public static void DownColumn(Pawn p, Color color)
        {
            var map = p?.Map;
            if (map == null || !p.Spawned) return;
            Vector3 to = p.DrawPos;
            Vector3 from = to + new Vector3(0f, 0f, 4.5f); // start above the target (north on screen)
            Color c = color; c.a = 0.3f;                   // 30% max
            MapComponent_SpellBeams.Add(map, from, to, c, Color.clear, BeamStyle.Ray, 16, 1.0f);
        }

        /// <summary>A murky dark puff over a pawn (death magic: Power Word Kill, Toll the Dead). Uses a
        /// Transparent fleck so a dark/black tint actually darkens.</summary>
        public static void DarkBurst(Pawn p, Color color)
        {
            var map = p?.Map;
            if (map == null || !p.Spawned) return;
            Vector3 loc = p.DrawPos;
            loc.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (!loc.ShouldSpawnMotesAt(map)) return;
            var d = FleckMaker.GetDataStatic(loc, map, RH_FleckDefOf.RH_Fleck_DarkBurst, 1.7f);
            Color c = color; if (c.a <= 0f) c.a = 0.75f;
            d.instanceColor = c;
            map.flecks.CreateFleck(d);
        }

        /// <summary>A fleck that briefly rises over the caster's head (e.g. Power Word Kill's spoken word).</summary>
        public static void CasterFleck(Pawn caster, FleckDef def, Color color)
        {
            var map = caster?.Map;
            if (map == null || !caster.Spawned || def == null) return;
            Vector3 loc = caster.DrawPos + new Vector3(0f, 0f, 0.75f);
            loc.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (!loc.ShouldSpawnMotesAt(map)) return;
            var d = FleckMaker.GetDataStatic(loc, map, def, 1.0f);
            Color c = color; if (c.a <= 0f) c.a = 1f;
            d.instanceColor = c;
            d.velocityAngle = 90f; d.velocitySpeed = 0.18f; // drift up gently
            map.flecks.CreateFleck(d);
        }

        /// <summary>Flings a ring of motes outward from a pawn (Dissonant Whispers, Toll the Dead pulse).</summary>
        public static void RadialBurst(Pawn p, FleckDef def, Color color, int count)
        {
            var map = p?.Map;
            if (map == null || !p.Spawned || def == null || count <= 0) return;
            Vector3 c0 = p.DrawPos;
            if (!c0.ShouldSpawnMotesAt(map)) return;
            for (int i = 0; i < count; i++)
            {
                var d = FleckMaker.GetDataStatic(c0, map, def, Rand.Range(0.5f, 0.8f));
                Color c = color; c.a = 0.85f;
                d.instanceColor = c;
                d.rotation = Rand.Range(0f, 360f);
                d.velocityAngle = (360f / count) * i + Rand.Range(-12f, 12f);
                d.velocitySpeed = Rand.Range(1.3f, 2.3f);
                map.flecks.CreateFleck(d);
            }
        }

        /// <summary>A quick claw-rake mark over a struck pawn (wildshape melee: Pounce, Rend).</summary>
        public static void ImpactSlash(Pawn p, FleckDef def, Color color)
        {
            var map = p?.Map;
            if (map == null || !p.Spawned || def == null) return;
            Vector3 loc = p.DrawPos;
            loc.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            if (!loc.ShouldSpawnMotesAt(map)) return;
            var d = FleckMaker.GetDataStatic(loc, map, def, Rand.Range(0.95f, 1.15f));
            Color c = color; if (c.a <= 0f) c.a = 0.9f;
            d.instanceColor = c;
            d.rotation = Rand.Range(-25f, 25f);
            map.flecks.CreateFleck(d);
        }

        /// <summary>Immediately desiccates a pawn's fresh corpse (Power Word Kill): jumps rot well past
        /// the desiccation threshold so the body withers at once.</summary>
        public static void Desiccate(Pawn p)
        {
            var corpse = p?.Corpse;
            var rot = corpse?.GetComp<CompRottable>();
            if (rot == null) return;
            rot.RotProgress = Mathf.Max(rot.RotProgress, 600000f); // ~10 days: past desiccation for any corpse
        }
    }
}
