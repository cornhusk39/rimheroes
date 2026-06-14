using System.Collections.Generic;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>Fighter's level-1 Fighting Style pick.</summary>
    public enum FighterStyle { None, Quickness, Strength, Cunning }

    /// <summary>
    /// A single hediff per hero, labeled with the class name (Fighter, Wizard, ...), whose tooltip
    /// enumerates every active class feature. Passive class effects that map to real pawn stats
    /// (e.g. Fighter's Indomitable toughness) are applied via a dynamically-built CurStage; effects
    /// that have no pawn stat (melee damage/speed/armor-pen) are applied by Harmony patches and only
    /// described here. Feats are NOT shown here - they have their own hediffs.
    /// </summary>
    public class Hediff_ClassFeatures : HediffWithComps
    {
        private HediffStage cachedStage;
        private int cachedKey = int.MinValue;

        private Hediff_HeroLevels Hero => HeroUtility.GetHeroHediff(pawn);

        public override bool ShouldRemove => false;
        public override bool Visible => true;

        public override string Label => Hero?.classDef?.label?.CapitalizeFirst() ?? base.Label;

        public override string LabelInBrackets => Hero != null ? "level " + Hero.level : null;

        public override HediffStage CurStage
        {
            get
            {
                var hero = Hero;
                int key = hero == null ? -1 : hero.level * 8 + (int)hero.FightingStyle;
                if (cachedStage == null || key != cachedKey)
                {
                    cachedStage = ClassFeatures.BuildStage(hero);
                    cachedKey = key;
                }
                return cachedStage;
            }
        }

        public override string TipStringExtra
        {
            get
            {
                string s = ClassFeatures.DescribeEffects(Hero);
                return string.IsNullOrEmpty(s) ? base.TipStringExtra : s;
            }
        }
    }

    /// <summary>
    /// Per-class feature math + descriptions, read by the class-features hediff (display + stat
    /// stage) and by the melee Harmony patch (combat numbers). Fighter is implemented; other classes
    /// fall through to a generic summary until their level-up pass.
    /// </summary>
    public static class ClassFeatures
    {
        public static bool IsFighter(Hediff_HeroLevels h) => h?.classDef?.defName == "RH_Fighter";

        // ----- Fighter tiers -----
        public static int ExtraAttackTier(int level) => level >= 20 ? 3 : level >= 11 ? 2 : level >= 5 ? 1 : 0;
        public static int IndomitableTier(int level) => level >= 17 ? 3 : level >= 13 ? 2 : level >= 9 ? 1 : 0;

        // ----- Fighter melee modifiers (no pawn stat exists for these; applied by Patch_FighterMelee) -----

        /// <summary>Multiplier on melee damage dealt (Strength style + Extra Attack tiers + Action Surge).</summary>
        public static float MeleeDamageFactor(Hediff_HeroLevels h)
        {
            if (!IsFighter(h)) return 1f;
            float f = 1f;
            if (h.FightingStyle == FighterStyle.Strength) f *= 1.15f;
            f *= 1f + 0.33f * ExtraAttackTier(h.level);
            if (ActionSurgeActive(h)) f *= h.level >= 17 ? 1.75f : 1.5f;
            return f;
        }

        /// <summary>Multiplier on melee attack cooldown (Quickness style = faster swings).</summary>
        public static float MeleeCooldownFactor(Hediff_HeroLevels h)
        {
            if (!IsFighter(h)) return 1f;
            float f = 1f;
            if (h.FightingStyle == FighterStyle.Quickness) f *= 0.80f;
            if (ActionSurgeActive(h)) f *= 0.80f;
            return f;
        }

        /// <summary>Flat armor-penetration added to melee hits (Cunning style).</summary>
        public static float MeleeArmorPenOffset(Hediff_HeroLevels h) =>
            IsFighter(h) && h.FightingStyle == FighterStyle.Cunning ? 0.20f : 0f;

        public static bool ActionSurgeActive(Hediff_HeroLevels h) =>
            h?.pawn?.health?.hediffSet?.HasHediff(RH_DefOf.RH_ActionSurge) == true;

        /// <summary>Death-saving-throw bonus from Indomitable.</summary>
        public static int DeathSaveBonus(Hediff_HeroLevels h)
        {
            int t = IsFighter(h) ? IndomitableTier(h.level) : 0;
            return t == 0 ? 0 : t + 1; // +2 / +3 / +4
        }

        // ----- Heroic growth: every class gains a little every level (no dead levels) -----

        // Universal per-level trickle (the "hit points" of 5e, mapped to RimWorld survivability + utility).
        public const float PainPerLevel = 0.01f;        // +pain-shock threshold (toughness)
        public const float ResistPerLevel = 0.005f;     // incoming-damage reduction
        public const float MovePerLevel = 0.01f;        // +1% move speed / level
        public const float WorkPerLevel = 0.02f;        // +2% work speed / level
        public const float SpellPowerPerLevel = 0.005f; // casters: +spell power / level

        private static StatDef Stat(string defName) => DefDatabase<StatDef>.GetNamedSilentFail(defName);

        // ----- Dynamic stat stage (heroic growth + Fighter's Indomitable toughness) -----

        public static HediffStage BuildStage(Hediff_HeroLevels h)
        {
            var stage = new HediffStage { statOffsets = new List<StatModifier>(), statFactors = new List<StatModifier>() };
            if (h?.classDef == null) return stage;
            int lvl = h.level;

            // Universal heroic growth, every level.
            stage.statOffsets.Add(new StatModifier { stat = StatDefOf.PainShockThreshold, value = PainPerLevel * lvl });
            stage.statFactors.Add(new StatModifier { stat = StatDefOf.MoveSpeed, value = 1f + MovePerLevel * lvl });
            stage.statFactors.Add(new StatModifier { stat = StatDefOf.WorkSpeedGlobal, value = 1f + WorkPerLevel * lvl });
            float incomingFactor = 1f - ResistPerLevel * lvl;

            // Fighter Indomitable toughness stacks on the baseline.
            if (IsFighter(h))
            {
                int t = IndomitableTier(lvl);
                if (t > 0)
                {
                    stage.statOffsets.Add(new StatModifier { stat = StatDefOf.PainShockThreshold, value = 0.05f + 0.10f * t });
                    var mbt = Stat("MentalBreakThreshold");
                    if (mbt != null) stage.statOffsets.Add(new StatModifier { stat = mbt, value = -0.05f - 0.05f * t });
                    incomingFactor *= 1f - 0.05f * t;
                }
            }

            var idf = Stat("IncomingDamageFactor");
            if (idf != null) stage.statFactors.Add(new StatModifier { stat = idf, value = incomingFactor });

            AddFlavorTrickle(h, lvl, stage);
            return stage;
        }

        /// <summary>A small class-flavored per-level stat trickle on top of the universal growth.</summary>
        private static void AddFlavorTrickle(Hediff_HeroLevels h, int lvl, HediffStage stage)
        {
            void off(StatDef s, float v) { if (s != null) stage.statOffsets.Add(new StatModifier { stat = s, value = v }); }
            switch (h.classDef.defName)
            {
                case "RH_Fighter":
                case "RH_Barbarian":
                case "RH_Paladin":
                    off(StatDefOf.MeleeHitChance, 0.3f * lvl);
                    break;
                case "RH_Monk":
                    off(StatDefOf.MeleeHitChance, 0.2f * lvl);
                    off(Stat("MeleeDodgeChance"), 0.3f * lvl);
                    break;
                case "RH_Rogue":
                    off(Stat("MeleeDodgeChance"), 0.4f * lvl);
                    off(StatDefOf.MeleeHitChance, 0.2f * lvl);
                    break;
                case "RH_Ranger":
                    off(Stat("ShootingAccuracyPawn"), 0.3f * lvl);
                    break;
                case "RH_Cleric":
                case "RH_Druid":
                case "RH_Wizard":
                case "RH_Sorcerer":
                case "RH_Bard":
                case "RH_Warlock":
                    off(RH_DefOf.RH_SpellPower, SpellPowerPerLevel * lvl);
                    break;
            }
        }

        // ----- Tooltip -----

        public static string DescribeEffects(Hediff_HeroLevels h)
        {
            if (h?.classDef == null) return null;
            int lvl = h.level;
            var sb = new StringBuilder();
            sb.AppendLine(GrowthLine(h, lvl));
            if (IsFighter(h))
            {
                sb.AppendLine(StyleLine(h.FightingStyle));
                sb.AppendLine("Second Wind: a self-mending burst on a cooldown.");
                if (lvl >= 2)
                    sb.AppendLine("Action Surge: a short surge of speed and striking power" + (lvl >= 17 ? " (improved)." : "."));
                int ea = ExtraAttackTier(lvl);
                if (ea > 0)
                    sb.AppendLine($"Extra Attack {Roman(ea)}: +{Mathf.RoundToInt(33f * ea)}% melee damage.");
                int ind = IndomitableTier(lvl);
                if (ind > 0)
                    sb.AppendLine($"Indomitable {Roman(ind)}: tougher, harder to break, and +{ind + 1} to death saves.");
            }
            return sb.ToString().TrimEndNewlines();
        }

        /// <summary>The universal "every level matters" line shown on every hero's class hediff.</summary>
        private static string GrowthLine(Hediff_HeroLevels h, int lvl)
        {
            string s = $"Heroic growth (level {lvl}): +{Mathf.RoundToInt(ResistPerLevel * 100f * lvl)}% damage resistance, " +
                       $"+{Mathf.RoundToInt(MovePerLevel * 100f * lvl)}% move speed, +{Mathf.RoundToInt(WorkPerLevel * 100f * lvl)}% work speed";
            switch (h.classDef.defName)
            {
                case "RH_Cleric":
                case "RH_Druid":
                case "RH_Wizard":
                case "RH_Sorcerer":
                case "RH_Bard":
                case "RH_Warlock":
                    s += $", +{Mathf.RoundToInt(SpellPowerPerLevel * 100f * lvl)}% spell power";
                    break;
                case "RH_Ranger":
                    s += $", +{(0.3f * lvl):0.#} shooting accuracy";
                    break;
                default:
                    s += $", +{(0.3f * lvl):0.#} melee";
                    break;
            }
            return s + ".";
        }

        private static string StyleLine(FighterStyle s)
        {
            switch (s)
            {
                case FighterStyle.Quickness: return "Fighting Style - Quickness: +20% attack speed.";
                case FighterStyle.Strength: return "Fighting Style - Strength: +15% melee damage.";
                case FighterStyle.Cunning: return "Fighting Style - Cunning: +20% armor penetration.";
                default: return "Fighting Style: not yet chosen.";
            }
        }

        private static string Roman(int n) => n == 1 ? "I" : n == 2 ? "II" : n == 3 ? "III" : n.ToString();
    }
}
