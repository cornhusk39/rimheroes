using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A hitscan spell that snaps a brief, unstable energy beam from caster to target and damages the
    /// target (scaled by Spell Power). Used by Eldritch Blast for its signature crackling red bolt.
    /// </summary>
    public class CompProperties_AbilityBeam : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public float amount = 14f;
        public Color color = Color.white;
        public int ticks = 24;       // how long the beam is visible
        public float width = 0.34f;

        public CompProperties_AbilityBeam() => compClass = typeof(CompAbilityEffect_Beam);
    }

    public class CompAbilityEffect_Beam : CompAbilityEffect
    {
        public new CompProperties_AbilityBeam Props => (CompProperties_AbilityBeam)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var caster = parent.pawn;
            var map = caster?.MapHeld;
            if (map == null) return;

            Vector3 to = target.HasThing ? target.Thing.DrawPos : target.Cell.ToVector3Shifted();
            MapComponent_SpellBeams.Add(map, caster.DrawPos, to, Props.color, Props.ticks, Props.width);

            if (target.Thing != null && !(target.Thing is Pawn dead && dead.Dead))
            {
                float dmg = Props.amount * SpellPower.For(caster);
                target.Thing.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Burn, dmg, 0.4f, -1f, caster));
            }
        }
    }

    /// <summary>
    /// Draws short-lived spell beams (a jagged, jittering energy line between two points) each frame.
    /// One auto-instantiates per map. The jitter is re-rolled every frame for an unstable, crackling
    /// look; the beam flickers and fades over its tick lifetime.
    /// </summary>
    public class MapComponent_SpellBeams : MapComponent
    {
        private class Beam
        {
            public Vector3 a, b;
            public Color color;
            public int ticksLeft, maxTicks;
            public float width;
        }

        private readonly List<Beam> beams = new List<Beam>();
        private static Material beamMat;
        private static readonly MaterialPropertyBlock Mpb = new MaterialPropertyBlock();

        public MapComponent_SpellBeams(Map map) : base(map) { }

        public static void Add(Map map, Vector3 from, Vector3 to, Color color, int ticks, float width = 0.34f)
        {
            map?.GetComponent<MapComponent_SpellBeams>()?.beams.Add(new Beam
            {
                a = from, b = to, color = color, ticksLeft = ticks, maxTicks = Mathf.Max(1, ticks), width = width
            });
        }

        public override void MapComponentTick()
        {
            for (int i = beams.Count - 1; i >= 0; i--)
                if (--beams[i].ticksLeft <= 0)
                    beams.RemoveAt(i);
        }

        public override void MapComponentUpdate()
        {
            if (beams.Count == 0) return;
            if (beamMat == null)
                beamMat = MaterialPool.MatFrom("Things/RimHeroes/Effects/Lightning", ShaderDatabase.MoteGlow);
            float y = AltitudeLayer.MoteOverhead.AltitudeFor();
            foreach (var beam in beams)
                Draw(beam, y);
        }

        private void Draw(Beam beam, float y)
        {
            Vector3 a = beam.a, b = beam.b;
            a.y = b.y = y;
            Vector3 dir = b - a; dir.y = 0f;
            float len = dir.magnitude;
            if (len < 0.05f) return;
            Vector3 fwd = dir / len;
            Vector3 perp = new Vector3(-fwd.z, 0f, fwd.x);

            float life = (float)beam.ticksLeft / beam.maxTicks;
            Color c = beam.color;
            c.a *= Mathf.Clamp01(life) * Rand.Range(0.55f, 1f);   // flicker + fade for instability
            Mpb.SetColor(ShaderPropertyIDs.Color, c);

            int segs = Mathf.Clamp(Mathf.RoundToInt(len / 1.1f), 3, 12);
            float jit = beam.width * 2.2f;
            Vector3 prev = a;
            for (int i = 1; i <= segs; i++)
            {
                float t = (float)i / segs;
                Vector3 point = i == segs
                    ? b
                    : a + fwd * (len * t) + perp * Rand.Range(-jit, jit);
                DrawSeg(prev, point, beam.width);
                prev = point;
            }
        }

        private void DrawSeg(Vector3 p0, Vector3 p1, float width)
        {
            Vector3 seg = p1 - p0;
            float l = seg.magnitude;
            if (l < 0.001f) return;
            float angle = Mathf.Atan2(-seg.z, seg.x) * Mathf.Rad2Deg;   // align the quad's X axis with the segment
            var matrix = Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.Euler(0f, angle, 0f),
                new Vector3(l + 0.06f, 1f, width));
            Graphics.DrawMesh(MeshPool.plane10, matrix, beamMat, 0, null, 0, Mpb);
        }
    }
}
