using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    public enum BeamStyle
    {
        Lightning,  // jagged, jittering, alpha-flicker (eldritch blast, lightning bolt, ...)
        Ray         // straight, thin, swells from nothing then shrinks away (frost/fire/radiant rays)
    }

    /// <summary>
    /// A hitscan ray/bolt spell: snaps an energy line from caster to target, deals damage (Spell-Power
    /// scaled) and optionally applies a hediff. The visual is either jagged Lightning or a clean Ray.
    /// Damage fields mirror CompProperties_AbilityDamageTarget so a spell can be swapped over by just
    /// changing the comp class and adding a colour + style.
    /// </summary>
    public class CompProperties_AbilityBeam : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public float amount = 14f;
        public int hits = 1;
        public HediffDef applyHediff;
        public float applyHediffSeverity = 1f;

        public Color color = Color.white;
        public Color innerColor = Color.clear; // optional second, narrower core pass (e.g. chaos bolt)
        public BeamStyle style = BeamStyle.Lightning;
        public int ticks = 24;
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
            MapComponent_SpellBeams.Add(map, caster.DrawPos, to, Props.color, Props.innerColor, Props.style, Props.ticks, Props.width);

            var thing = target.Thing;
            if (thing == null || (thing is Pawn dead && dead.Dead)) return;
            float dmg = Props.amount * SpellPower.For(caster);
            for (int i = 0; i < Mathf.Max(1, Props.hits); i++)
                thing.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Burn, dmg, 0.4f, -1f, caster));
            if (Props.applyHediff != null && thing is Pawn p && !p.Dead)
            {
                var h = p.health.AddHediff(Props.applyHediff);
                if (h != null) h.Severity = Props.applyHediffSeverity;
            }
        }
    }

    /// <summary>Call Lightning: drops a real in-game lightning strike on the target cell (bolt + thunder)
    /// and deals Spell-Power-scaled damage plus an optional hediff to the target.</summary>
    public class CompProperties_AbilityCallLightning : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public float amount = 24f;
        public HediffDef applyHediff;
        public float applyHediffSeverity = 1f;

        public CompProperties_AbilityCallLightning() => compClass = typeof(CompAbilityEffect_CallLightning);
    }

    public class CompAbilityEffect_CallLightning : CompAbilityEffect
    {
        public new CompProperties_AbilityCallLightning Props => (CompProperties_AbilityCallLightning)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var caster = parent.pawn;
            var map = caster?.MapHeld;
            if (map == null) return;
            map.weatherManager.eventHandler.AddEvent(new WeatherEvent_LightningStrike(map, target.Cell));
            if (target.Thing is Pawn p && !p.Dead)
            {
                if (Props.amount > 0f)
                    p.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Burn, Props.amount * SpellPower.For(caster), 0.4f, -1f, caster));
                if (Props.applyHediff != null)
                {
                    var h = p.health.AddHediff(Props.applyHediff);
                    if (h != null) h.Severity = Props.applyHediffSeverity;
                }
            }
        }
    }

    /// <summary>
    /// Draws short-lived spell beams each frame. Lightning beams are jagged, re-jittered every frame and
    /// alpha-flickering (unstable). Ray beams are straight and thin, swelling from nothing to full then
    /// shrinking away (a "spawn from nothing" pulse). One component auto-instantiates per map.
    /// </summary>
    public class MapComponent_SpellBeams : MapComponent
    {
        private class Beam
        {
            public Vector3 a, b;
            public Color color, innerColor;
            public BeamStyle style;
            public int ticksLeft, maxTicks;
            public float width;
        }

        private readonly List<Beam> beams = new List<Beam>();
        private static Material beamMat;
        private static readonly MaterialPropertyBlock Mpb = new MaterialPropertyBlock();
        private static readonly List<Vector3> Pts = new List<Vector3>();

        public MapComponent_SpellBeams(Map map) : base(map) { }

        public static void Add(Map map, Vector3 from, Vector3 to, Color color, Color innerColor,
            BeamStyle style, int ticks, float width)
        {
            map?.GetComponent<MapComponent_SpellBeams>()?.beams.Add(new Beam
            {
                a = from, b = to, color = color, innerColor = innerColor, style = style,
                ticksLeft = ticks, maxTicks = Mathf.Max(1, ticks), width = width
            });
        }

        // back-compat single-colour lightning overload
        public static void Add(Map map, Vector3 from, Vector3 to, Color color, int ticks, float width = 0.34f) =>
            Add(map, from, to, color, Color.clear, BeamStyle.Lightning, ticks, width);

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
            {
                if (beam.style == BeamStyle.Ray) DrawRay(beam, y);
                else DrawLightning(beam, y);
            }
        }

        private void DrawLightning(Beam beam, float y)
        {
            Vector3 a = beam.a, b = beam.b;
            a.y = b.y = y;
            Vector3 dir = b - a; dir.y = 0f;
            float len = dir.magnitude;
            if (len < 0.05f) return;
            Vector3 fwd = dir / len;
            Vector3 perp = new Vector3(-fwd.z, 0f, fwd.x);

            float life = (float)beam.ticksLeft / beam.maxTicks;
            float flicker = Mathf.Clamp01(life) * Rand.Range(0.55f, 1f);

            int segs = Mathf.Clamp(Mathf.RoundToInt(len / 1.1f), 3, 12);
            float jit = beam.width * 2.2f;
            Pts.Clear();
            Pts.Add(a);
            for (int i = 1; i < segs; i++)
                Pts.Add(a + fwd * (len * i / segs) + perp * Rand.Range(-jit, jit));
            Pts.Add(b);

            DrawPolyline(beam.color, flicker, beam.width);
            if (beam.innerColor.a > 0f)
                DrawPolyline(beam.innerColor, flicker, beam.width * 0.45f);
        }

        private void DrawPolyline(Color color, float alphaMul, float width)
        {
            Color c = color; c.a *= alphaMul;
            Mpb.SetColor(ShaderPropertyIDs.Color, c);
            for (int i = 1; i < Pts.Count; i++)
                DrawSeg(Pts[i - 1], Pts[i], width);
        }

        private void DrawRay(Beam beam, float y)
        {
            Vector3 a = beam.a, b = beam.b;
            a.y = b.y = y;
            float p = 1f - (float)beam.ticksLeft / beam.maxTicks; // 0 -> 1 over life
            float bump = Mathf.Sin(Mathf.Clamp01(p) * Mathf.PI);   // 0 -> 1 -> 0 (swell from nothing)
            if (bump <= 0.001f) return;
            Color c = beam.color; c.a *= bump;
            Mpb.SetColor(ShaderPropertyIDs.Color, c);
            DrawSeg(a, b, beam.width * (0.15f + 0.85f * bump));    // thin, swelling
        }

        private void DrawSeg(Vector3 p0, Vector3 p1, float width)
        {
            Vector3 seg = p1 - p0;
            float l = seg.magnitude;
            if (l < 0.001f) return;
            float angle = Mathf.Atan2(-seg.z, seg.x) * Mathf.Rad2Deg;
            var matrix = Matrix4x4.TRS((p0 + p1) * 0.5f, Quaternion.Euler(0f, angle, 0f),
                new Vector3(l + 0.06f, 1f, width));
            Graphics.DrawMesh(MeshPool.plane10, matrix, beamMat, 0, null, 0, Mpb);
        }
    }
}
