using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The Hero progression engine, one per Hero pawn (VPE Hediff_PsycastAbilities pattern).
    /// Stores class, level, XP; applies per-level grants. Will grow: points, known spells,
    /// slot state, vestment state, gestral roster.
    /// </summary>
    public class Hediff_HeroLevels : HediffWithComps
    {
        public HeroClassDef classDef;
        public int level = 1;
        public float xp;

        public override string LabelInBrackets => classDef != null ? $"{classDef.label} {level}" : base.LabelInBrackets;

        public override bool ShouldRemove => false;

        // Placeholder curve (VPE-style: 100 XP at L1, x1.15 per level). Tune in playtest.
        public static float XPRequiredForLevel(int lvl) => 100f * Mathf.Pow(1.15f, lvl - 1);

        public float XPForNextLevel => XPRequiredForLevel(level);

        public bool AtMaxLevel => classDef == null || level >= classDef.maxLevel;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            ApplyGrants();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            // Passive "surviving the rim" trickle; VTR-safe via delta-aware hash interval.
            if (pawn.IsHashIntervalTick(RH_Tuning.PassiveXPIntervalTicks, delta))
            {
                GainXP(RH_Tuning.PassiveXPPerHour);
            }
        }

        public void GainXP(float amount)
        {
            if (classDef == null || amount <= 0f || AtMaxLevel)
            {
                return;
            }
            xp += amount;
            bool leveled = false;
            while (!AtMaxLevel && xp >= XPRequiredForLevel(level))
            {
                xp -= XPRequiredForLevel(level);
                level++;
                leveled = true;
            }
            if (leveled)
            {
                ApplyGrants();
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message("RH_LeveledUp".Translate(pawn.LabelShortCap, classDef.label, level),
                        pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
            if (AtMaxLevel)
            {
                xp = 0f;
            }
        }

        /// <summary>Idempotent: applies every grant at or below the current level.</summary>
        private void ApplyGrants()
        {
            if (classDef?.levelGrants == null)
            {
                return;
            }
            foreach (var grant in classDef.levelGrants)
            {
                if (grant.level > level)
                {
                    continue;
                }
                if (grant.abilities != null)
                {
                    foreach (var abilityDef in grant.abilities)
                    {
                        pawn.abilities?.GainAbility(abilityDef); // dup-safe in vanilla
                    }
                }
                if (grant.features != null)
                {
                    foreach (var featureDef in grant.features)
                    {
                        if (!pawn.health.hediffSet.HasHediff(featureDef))
                        {
                            pawn.health.AddHediff(featureDef);
                        }
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref classDef, "classDef");
            Scribe_Values.Look(ref level, "level", 1);
            Scribe_Values.Look(ref xp, "xp");
        }
    }
}
