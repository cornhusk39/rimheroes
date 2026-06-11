using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The Hero progression engine, one per Hero pawn (VPE Hediff_PsycastAbilities pattern).
    /// Stores class, level, XP. Will grow: points, known spells, slot state, vestment state, gestral roster.
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

        public void GainXP(float amount)
        {
            if (classDef == null || amount <= 0f || level >= classDef.maxLevel) return;
            xp += amount;
            while (level < classDef.maxLevel && xp >= XPRequiredForLevel(level))
            {
                xp -= XPRequiredForLevel(level);
                level++;
                Notify_LevelUp();
            }
        }

        private void Notify_LevelUp()
        {
            // TODO: apply HeroLevelGrant for the new level (abilities, feature hediffs, gestral unlocks, vestment tier).
            if (PawnUtility.ShouldSendNotificationAbout(pawn))
            {
                Messages.Message($"{pawn.LabelShortCap} reached {classDef.label} level {level}!",
                    pawn, MessageTypeDefOf.PositiveEvent);
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
