using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Persistent VFX carried by a buff/debuff hediff for as long as it is held: an optional soft glow
    /// drawn on the pawn each frame (rendered by the aura render hook in AuraFx) and/or a periodic mote
    /// emission. Lets spells like Shield of Faith (shimmer), Holy Aura (corona), Searing Smite (embers),
    /// Spirit Guardians (wheeling wisps), Entangle (vines), and Darkness (dark wisps) show on the
    /// affected pawn without bespoke code per spell.
    /// </summary>
    public class HediffCompProperties_Vfx : HediffCompProperties
    {
        public Color glowColor = Color.clear;   // additive on-body glow (a>0 to enable)
        public float glowSize = 2.0f;
        public FleckDef mote;                    // periodic mote (null = none)
        public Color moteColor = Color.white;
        public int moteInterval = 20;            // ticks between emissions
        public int moteCount = 1;
        public float moteRadius = 0.5f;          // spread around the pawn
        public bool moteRise = true;             // true = drift up; false = swirl in place

        public HediffCompProperties_Vfx() => compClass = typeof(HediffComp_Vfx);
    }

    public class HediffComp_Vfx : HediffComp
    {
        public HediffCompProperties_Vfx Props => (HediffCompProperties_Vfx)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            var pawn = Pawn;
            if (Props.mote == null || pawn == null || !pawn.Spawned || pawn.Map == null) return;
            if (!pawn.IsHashIntervalTick(Props.moteInterval)) return;
            for (int i = 0; i < Props.moteCount; i++)
            {
                Vector3 loc = pawn.DrawPos + new Vector3(Rand.Range(-Props.moteRadius, Props.moteRadius), 0f,
                    Rand.Range(-Props.moteRadius, Props.moteRadius) + 0.1f);
                if (!loc.ShouldSpawnMotesAt(pawn.Map)) continue;
                var d = FleckMaker.GetDataStatic(loc, pawn.Map, Props.mote, Rand.Range(0.5f, 0.75f));
                Color c = Props.moteColor; c.a = 0.8f;
                d.instanceColor = c;
                d.rotation = Rand.Range(0f, 360f);
                if (Props.moteRise) { d.velocityAngle = Rand.Range(70f, 110f); d.velocitySpeed = Rand.Range(0.3f, 0.6f); }
                else { d.velocityAngle = Rand.Range(0f, 360f); d.velocitySpeed = Rand.Range(0.12f, 0.4f); }
                pawn.Map.flecks.CreateFleck(d);
            }
        }
    }

    /// <summary>
    /// Wires the persistent VFX onto the Batch-4 buff/debuff hediffs at startup, by injecting a
    /// HediffComp_Vfx (with the per-spell colour/mote config) into each def's comp list. Done in code
    /// rather than per-def XML so the whole table lives in one place and stays easy to retune.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class VfxHediffSetup
    {
        static VfxHediffSetup()
        {
            // On-body auras (soft glow, no motes).
            Add("RH_Shielded",    glow: new Color(0.95f, 0.85f, 0.50f, 0.50f), glowSize: 1.9f); // Shield of Faith
            Add("RH_HolyWard",    glow: new Color(1.00f, 0.95f, 0.78f, 0.50f), glowSize: 2.5f); // Holy Aura
            Add("RH_MirrorImage", glow: new Color(0.60f, 0.80f, 1.00f, 0.45f), glowSize: 2.0f); // Mirror Image

            // Weapon enchants (small glow + rising sparks/embers near the hands).
            Add("RH_SearingSmite", glow: new Color(1.00f, 0.45f, 0.12f, 0.30f), glowSize: 1.3f,
                mote: "RH_Mote_Spark", moteColor: new Color(1.00f, 0.50f, 0.15f), interval: 14, radius: 0.45f, rise: true);
            Add("RH_MagicWeapon",  glow: new Color(0.50f, 0.70f, 1.00f, 0.28f), glowSize: 1.3f,
                mote: "RH_Mote_Spark", moteColor: new Color(0.55f, 0.72f, 1.00f), interval: 16, radius: 0.45f, rise: true);

            // Area effects, drawn on each afflicted pawn (swirling motes; a faint glow where it suits).
            Add("RH_Harried",   glow: new Color(0.75f, 0.82f, 1.00f, 0.30f), glowSize: 1.7f,
                mote: "RH_Mote_Spark", moteColor: new Color(0.80f, 0.85f, 1.00f), interval: 12, radius: 0.70f, rise: false); // Spirit Guardians
            Add("RH_Blinded",  mote: "RH_AuraMote", moteColor: new Color(0.32f, 0.12f, 0.40f), interval: 10, radius: 0.60f, rise: false); // Darkness
            Add("RH_Devoured", mote: "RH_AuraMote", moteColor: new Color(0.22f, 0.09f, 0.32f), interval: 8,  radius: 0.70f, rise: false); // Hunger of Hadar
            Add("RH_Entangled",mote: "RH_AuraMote", moteColor: new Color(0.30f, 0.70f, 0.25f), interval: 14, radius: 0.50f, rise: false); // Entangle
            Add("RH_Spiked",   mote: "RH_AuraMote", moteColor: new Color(0.50f, 0.50f, 0.20f), interval: 16, radius: 0.50f, rise: false); // Spike Growth
            Add("RH_Dazed",    glow: new Color(0.85f, 0.40f, 0.95f, 0.30f), glowSize: 1.6f,
                mote: "RH_Mote_Spark", moteColor: new Color(0.90f, 0.50f, 1.00f), interval: 12, radius: 0.60f, rise: false); // Hypnotic Pattern

            // ===== Self/ally buffs (warm glow, rising sparks) =====
            Add("RH_Hasted",    glow: new Color(1.00f, 0.95f, 0.45f, 0.35f), glowSize: 1.7f,
                mote: "RH_Mote_Spark", moteColor: new Color(1.00f, 0.95f, 0.55f), interval: 7, radius: 0.50f, rise: true);  // Haste
            Add("RH_Blessed",   glow: new Color(1.00f, 0.93f, 0.70f, 0.40f), glowSize: 1.8f,
                mote: "RH_Mote_Spark", moteColor: new Color(1.00f, 0.95f, 0.75f), interval: 18, radius: 0.50f, rise: true); // Bless
            Add("RH_Aided",     glow: new Color(0.55f, 0.95f, 0.55f, 0.32f), glowSize: 1.7f);                                // Aid
            Add("RH_MageArmor", glow: new Color(0.45f, 0.65f, 1.00f, 0.30f), glowSize: 1.6f);                                // Mage Armor
            Add("RH_DeathWard", glow: new Color(0.95f, 0.95f, 0.80f, 0.35f), glowSize: 1.9f);                                // Death Ward
            Add("RH_Guided",    glow: new Color(0.85f, 0.90f, 1.00f, 0.25f), glowSize: 1.5f);                                // Guidance
            Add("RH_Couraged",  glow: new Color(1.00f, 0.82f, 0.40f, 0.30f), glowSize: 1.7f);                                // Aura of Courage (ally)
            Add("RH_Heroism",   glow: new Color(1.00f, 0.75f, 0.35f, 0.35f), glowSize: 1.8f);                                // Heroism
            Add("RH_Rage",      glow: new Color(0.95f, 0.25f, 0.18f, 0.38f), glowSize: 1.9f,
                mote: "RH_Mote_Spark", moteColor: new Color(1.00f, 0.40f, 0.20f), interval: 12, radius: 0.60f, rise: true); // Barbarian Rage
            Add("RH_Overcharge",glow: new Color(0.75f, 0.45f, 1.00f, 0.40f), glowSize: 1.8f,
                mote: "RH_Mote_Spark", moteColor: new Color(0.85f, 0.55f, 1.00f), interval: 9, radius: 0.55f, rise: true);  // Font of Magic
            Add("RH_Inspired",  glow: new Color(0.55f, 0.85f, 1.00f, 0.30f), glowSize: 1.6f,
                mote: "RH_AuraMote", moteColor: new Color(0.70f, 0.90f, 1.00f), interval: 16, radius: 0.50f, rise: true);   // Bardic Inspiration
            Add("RH_BardicInspiration", glow: new Color(0.55f, 0.85f, 1.00f, 0.30f), glowSize: 1.6f,
                mote: "RH_AuraMote", moteColor: new Color(0.70f, 0.90f, 1.00f), interval: 16, radius: 0.50f, rise: true);

            // ===== Debuffs (swirling motes; cold/dark glows) =====
            Add("RH_Slowed",    glow: new Color(0.30f, 0.35f, 0.55f, 0.30f), glowSize: 1.6f,
                mote: "RH_AuraMote", moteColor: new Color(0.40f, 0.45f, 0.65f), interval: 16, radius: 0.55f, rise: false);  // Slow
            Add("RH_Chilled",   mote: "RH_AuraMote", moteColor: new Color(0.65f, 0.85f, 1.00f), interval: 14, radius: 0.55f, rise: false); // cold
            Add("RH_Jolted",    glow: new Color(0.70f, 0.85f, 1.00f, 0.35f), glowSize: 1.6f,
                mote: "RH_Mote_Spark", moteColor: new Color(0.80f, 0.92f, 1.00f), interval: 6, radius: 0.55f, rise: false); // lightning
            Add("RH_Held",      glow: new Color(1.00f, 0.85f, 0.35f, 0.35f), glowSize: 1.6f,
                mote: "RH_AuraMote", moteColor: new Color(1.00f, 0.88f, 0.45f), interval: 18, radius: 0.45f, rise: false);  // Hold Person/Monster
            Add("RH_Confused",  mote: "RH_AuraMote", moteColor: new Color(0.75f, 0.45f, 0.95f), interval: 12, radius: 0.60f, rise: false); // Confusion
            Add("RH_Frightened",mote: "RH_AuraMote", moteColor: new Color(0.45f, 0.20f, 0.55f), interval: 12, radius: 0.60f, rise: false); // Fear
            Add("RH_Baned",     mote: "RH_AuraMote", moteColor: new Color(0.55f, 0.15f, 0.20f), interval: 14, radius: 0.55f, rise: false); // Bane
            Add("RH_Marked",    mote: "RH_AuraMote", moteColor: new Color(0.85f, 0.30f, 0.25f), interval: 18, radius: 0.45f, rise: false); // Hunter's Mark
            Add("RH_Snared",    mote: "RH_AuraMote", moteColor: new Color(0.35f, 0.65f, 0.25f), interval: 14, radius: 0.50f, rise: false); // Ensnaring Strike
            Add("RH_Stunned",   mote: "RH_Mote_Spark", moteColor: new Color(1.00f, 0.95f, 0.55f), interval: 8, radius: 0.45f, rise: false); // Stun

            // Summons: the spiritual weapon carries a soft radiant-gold aura while it persists.
            Add("RH_SummonLifespan", glow: new Color(1.00f, 0.93f, 0.55f, 0.45f), glowSize: 1.8f);
        }

        static void Add(string defName, Color glow = default, float glowSize = 2.0f, string mote = null,
            Color moteColor = default, int interval = 20, float radius = 0.5f, bool rise = true)
        {
            var def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (def == null) { Log.Warning($"[RimHeroes] VfxHediffSetup: hediff '{defName}' not found, skipping."); return; }
            var p = new HediffCompProperties_Vfx
            {
                glowColor = glow,
                glowSize = glowSize,
                mote = mote != null ? DefDatabase<FleckDef>.GetNamedSilentFail(mote) : null,
                moteColor = moteColor == default ? Color.white : moteColor,
                moteInterval = interval,
                moteRadius = radius,
                moteRise = rise
            };
            if (def.comps == null) def.comps = new List<HediffCompProperties>();
            def.comps.Add(p);
        }
    }
}
