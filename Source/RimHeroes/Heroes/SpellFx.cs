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
    }
}
