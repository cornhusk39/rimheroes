using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>Fighter's level-1 Fighting Style pick.</summary>
    public enum FighterStyle { None, Quickness, Strength, Cunning }

    /// <summary>Ranger's level-1 Favored Enemy pick.</summary>
    public enum FavoredEnemy { None, Beasts, Mechanoids, Humanlikes, Insects }

    /// <summary>Warlock's level-3 Pact Boon pick. (Chain familiar pending a design decision.)</summary>
    public enum PactBoon { None, Blade, Tome }

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

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            if (pawn.IsHashIntervalTick(120, delta))
            {
                ClassFeatures.TickAuras(Hero);
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

        // ----- Shared tier helpers -----
        public static int BrutalCritTier(int level) => level >= 17 ? 3 : level >= 13 ? 2 : level >= 9 ? 1 : 0;
        private static int OneAt(int level, int unlock) => level >= unlock ? 1 : 0;

        private static bool Has(Hediff_HeroLevels h, HediffDef def) =>
            def != null && h?.pawn?.health?.hediffSet?.HasHediff(def) == true;

        public static bool ActionSurgeActive(Hediff_HeroLevels h) => Has(h, RH_DefOf.RH_ActionSurge);

        // ----- Melee modifiers (no pawn stat exists for these; applied by Patch_FighterMelee) -----

        /// <summary>Unconditional melee-damage multiplier per class (tiers + active burst hediffs).</summary>
        public static float MeleeDamageFactor(Hediff_HeroLevels h)
        {
            if (h?.classDef == null) return 1f;
            int lvl = h.level;
            float f = 1f;
            switch (h.classDef.defName)
            {
                case "RH_Fighter":
                    if (h.FightingStyle == FighterStyle.Strength) f *= 1.15f;
                    f *= 1f + 0.33f * ExtraAttackTier(lvl);
                    if (Has(h, RH_DefOf.RH_ActionSurge)) f *= lvl >= 17 ? 1.75f : 1.5f;
                    break;
                case "RH_Barbarian":
                    f *= 1f + 0.33f * OneAt(lvl, 5);            // Extra Attack
                    f *= 1f + 0.15f * BrutalCritTier(lvl);      // Brutal Critical
                    if (lvl >= 20) f *= 1.25f;                  // Primal Champion
                    if (Has(h, RH_DefOf.RH_Rage)) f *= 1.5f;    // Rage
                    break;
                case "RH_Monk":
                    f *= 1f + 0.006f * lvl;                     // Martial Arts scaling
                    f *= 1f + 0.33f * OneAt(lvl, 5);            // Extra Attack
                    if (Has(h, RH_DefOf.RH_Flurry)) f *= 1.4f;  // Flurry of Blows
                    break;
                case "RH_Paladin":
                    f *= 1f + 0.33f * OneAt(lvl, 5);            // Extra Attack
                    if (lvl >= 11) f *= 1.25f;                  // Improved Divine Smite
                    break;
                case "RH_Ranger":
                    f *= 1f + 0.33f * OneAt(lvl, 5);            // Extra Attack (melee side)
                    break;
                case "RH_Rogue":
                    if (lvl >= 20) f *= 1.15f;                  // Stroke of Luck
                    break;
                case "RH_Warlock":
                    if (h.pactBoon == PactBoon.Blade) f *= 1.25f; // Pact of the Blade
                    break;
            }
            return f;
        }

        /// <summary>Conditional melee-damage multiplier that needs the target (Sneak Attack, Favored Enemy).</summary>
        public static float MeleeDamageVsTarget(Hediff_HeroLevels h, Thing target)
        {
            if (h?.classDef == null) return 1f;
            switch (h.classDef.defName)
            {
                case "RH_Rogue":
                    if (IsFlanked(h.pawn, target)) return 1f + 0.05f * h.level; // Sneak Attack
                    break;
                case "RH_Ranger":
                    if (IsFavoredEnemy(h, target)) return 1f + (h.level >= 20 ? 0.06f : 0.04f) * h.level; // Favored Enemy / Foe Slayer
                    break;
            }
            return 1f;
        }

        /// <summary>5e "advantage": the target is downed/asleep or focused on someone other than the rogue.</summary>
        public static bool IsFlanked(Pawn attacker, Thing target)
        {
            if (!(target is Pawn tp)) return false;
            if (tp.Downed || !tp.Awake()) return true;
            var foe = tp.mindState?.enemyTarget;
            return foe != null && foe != attacker;
        }

        public static bool IsFavoredEnemy(Hediff_HeroLevels h, Thing target)
        {
            if (!(target is Pawn tp) || h.favoredEnemy == FavoredEnemy.None) return false;
            switch (h.favoredEnemy)
            {
                case FavoredEnemy.Beasts: return tp.RaceProps?.Animal == true;
                case FavoredEnemy.Mechanoids: return tp.RaceProps?.IsMechanoid == true;
                case FavoredEnemy.Humanlikes: return tp.RaceProps?.Humanlike == true;
                case FavoredEnemy.Insects: return tp.RaceProps?.Insect == true;
                default: return false;
            }
        }

        /// <summary>Multiplier on melee attack cooldown (faster swings).</summary>
        public static float MeleeCooldownFactor(Hediff_HeroLevels h)
        {
            if (h?.classDef == null) return 1f;
            float f = 1f;
            if (h.classDef.defName == "RH_Fighter")
            {
                if (h.FightingStyle == FighterStyle.Quickness) f *= 0.80f;
                if (Has(h, RH_DefOf.RH_ActionSurge)) f *= 0.80f;
            }
            if (Has(h, RH_DefOf.RH_Flurry)) f *= 0.65f;  // Monk Flurry of Blows
            return f;
        }

        /// <summary>Flat armor-penetration added to melee hits.</summary>
        public static float MeleeArmorPenOffset(Hediff_HeroLevels h)
        {
            if (h?.classDef == null) return 0f;
            float p = 0f;
            if (h.classDef.defName == "RH_Fighter" && h.FightingStyle == FighterStyle.Cunning) p += 0.20f;
            if (h.classDef.defName == "RH_Barbarian") p += 0.05f * BrutalCritTier(h.level); // Brutal Critical
            return p;
        }

        /// <summary>Death-saving-throw bonus (Fighter Indomitable, Barbarian Relentless Rage while raging).</summary>
        public static int DeathSaveBonus(Hediff_HeroLevels h)
        {
            if (h?.classDef == null) return 0;
            int b = 0;
            if (IsFighter(h)) { int t = IndomitableTier(h.level); if (t > 0) b += t + 1; }
            if (h.classDef.defName == "RH_Barbarian" && h.level >= 11 && Has(h, RH_DefOf.RH_Rage)) b += 5; // Relentless Rage
            return b;
        }

        // ----- Passive ally-auras (Paladin; later Bard/Cleric). Refreshed by the class-hediff tick. -----

        private const int AuraRefreshTicks = 240;

        /// <summary>Called periodically by Hediff_ClassFeatures: refresh this hero's auras on nearby allies.</summary>
        public static void TickAuras(Hediff_HeroLevels h)
        {
            if (h?.pawn == null || !h.pawn.Spawned || h.pawn.Dead || h.pawn.Map == null) return;
            int lvl = h.level;
            switch (h.classDef.defName)
            {
                case "RH_Paladin":
                    float r = lvl >= 18 ? 7.9f : 4.9f;
                    if (lvl >= 6) Emit(h.pawn, RH_DefOf.RH_AuraProtection, r);
                    if (lvl >= 10) Emit(h.pawn, RH_DefOf.RH_AuraCourage, r);
                    break;
                case "RH_Bard":
                    if (lvl >= 6) Emit(h.pawn, RH_DefOf.RH_AuraCourage, 5.9f); // Countercharm
                    break;
            }
        }

        private static void Emit(Pawn source, HediffDef def, float radius)
        {
            if (def == null) return;
            foreach (var p in GenRadial.RadialDistinctThingsAround(source.Position, source.Map, radius, true).OfType<Pawn>())
            {
                if (p.Dead || p.HostileTo(source) || (p.RaceProps?.Humanlike != true && p.RaceProps?.Animal != true)) continue;
                var existing = p.health.hediffSet.GetFirstHediffOfDef(def);
                if (existing == null)
                {
                    p.health.AddHediff(def);
                    existing = p.health.hediffSet.GetFirstHediffOfDef(def);
                }
                var dis = (existing as HediffWithComps)?.TryGetComp<HediffComp_Disappears>();
                if (dis != null) dis.ticksToDisappear = AuraRefreshTicks + 60;
            }
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

            // Incoming-damage resistance: universal + class (combined into one factor to avoid double-multiply).
            var idf = Stat("IncomingDamageFactor");
            if (idf != null)
                stage.statFactors.Add(new StatModifier { stat = idf, value = (1f - ResistPerLevel * lvl) * ClassIncomingDamageMult(h, lvl) });

            AddFlavorTrickle(h, lvl, stage);
            AddClassPassives(h, lvl, stage);
            return stage;
        }

        /// <summary>Class passive incoming-damage multipliers (Fighter Indomitable, Monk/Rogue Evasion, etc.).</summary>
        private static float ClassIncomingDamageMult(Hediff_HeroLevels h, int lvl)
        {
            float m = 1f;
            switch (h.classDef.defName)
            {
                case "RH_Fighter": { int t = IndomitableTier(lvl); if (t > 0) m *= 1f - 0.05f * t; break; }
                case "RH_Monk": if (lvl >= 7) m *= 0.90f; break;                         // Evasion
                case "RH_Rogue": if (lvl >= 5) m *= 0.90f; if (lvl >= 7) m *= 0.90f; break; // Uncanny Dodge + Evasion
            }
            return m;
        }

        /// <summary>Class milestone passives expressed as pawn-stat offsets (armor, dodge, immunity, mind, etc.).</summary>
        private static void AddClassPassives(Hediff_HeroLevels h, int lvl, HediffStage stage)
        {
            void off(StatDef s, float v) { if (s != null && v != 0f) stage.statOffsets.Add(new StatModifier { stat = s, value = v }); }
            var sharp = StatDefOf.ArmorRating_Sharp;
            var blunt = StatDefOf.ArmorRating_Blunt;
            var dodge = Stat("MeleeDodgeChance");
            var mbt = Stat("MentalBreakThreshold");
            var imm = Stat("ImmunityGainSpeed");
            switch (h.classDef.defName)
            {
                case "RH_Fighter": { int t = IndomitableTier(lvl); if (t > 0) { off(StatDefOf.PainShockThreshold, 0.05f + 0.10f * t); off(mbt, -0.05f - 0.05f * t); } break; }

                case "RH_Barbarian":
                    off(sharp, 0.10f); off(blunt, 0.15f);                  // Unarmored Defense
                    if (lvl >= 2) off(dodge, 4f);                          // Danger Sense
                    if (lvl >= 5) off(StatDefOf.MoveSpeed, 0.4f);          // Fast Movement (flat, on top of % baseline)
                    if (lvl >= 18) off(StatDefOf.PainShockThreshold, 0.15f); // Indomitable Might toughness
                    if (lvl >= 20) { off(StatDefOf.PainShockThreshold, 0.10f); off(mbt, -0.10f); } // Primal Champion
                    break;

                case "RH_Monk":
                    off(sharp, 0.10f); off(blunt, 0.10f);                  // Unarmored Defense
                    off(StatDefOf.MoveSpeed, lvl >= 9 ? 0.6f : 0.3f);      // Unarmored Movement
                    if (lvl >= 3) off(dodge, 3f);                          // Deflect Missiles (approx)
                    if (lvl >= 10) off(imm, 2.0f);                         // Purity of Body
                    if (lvl >= 14) off(mbt, -0.10f);                       // Diamond Soul
                    break;

                case "RH_Rogue":
                    off(dodge, 4f);                                        // base nimbleness
                    if (lvl >= 15) off(mbt, -0.10f);                       // Slippery Mind
                    if (lvl >= 18) off(dodge, 8f);                         // Elusive
                    off(Stat("GlobalLearningFactor"), 0.30f);             // Expertise
                    break;

                case "RH_Ranger":
                    if (lvl >= 8) off(StatDefOf.MoveSpeed, 0.3f);          // Land's Stride
                    if (lvl >= 18) off(Stat("ShootingAccuracyPawn"), 3f); // Feral Senses
                    break;

                case "RH_Paladin":
                    off(sharp, 0.10f); off(blunt, 0.10f);                  // Fighting Style: Defense
                    if (lvl >= 3) off(imm, 2.0f);                          // Divine Health
                    break;

                case "RH_Druid":
                    off(Stat("PlantWorkSpeed"), 0.3f);                     // nature affinity
                    off(StatDefOf.MoveSpeed, 0.2f);
                    break;
                case "RH_Bard":
                    off(Stat("GlobalLearningFactor"), 0.3f);              // Jack of All Trades
                    off(Stat("SocialImpact"), 0.25f);
                    break;
                case "RH_Sorcerer":
                    off(RH_DefOf.RH_SpellPower, 0.10f);                    // Font of Magic (innate power)
                    break;
                case "RH_Warlock":
                    if (h.pactBoon == PactBoon.Tome) off(RH_DefOf.RH_SpellPower, 0.12f); // Pact of the Tome
                    break;
            }
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
            else switch (h.classDef.defName)
            {
                case "RH_Barbarian":
                    sb.AppendLine("Unarmored Defense: tougher skin (armor) and Danger Sense (dodge).");
                    sb.AppendLine("Rage: a fury that boosts damage and shrugs off blows" + (lvl >= 15 ? " (lasts longer)." : "."));
                    if (lvl >= 5) sb.AppendLine($"Extra Attack + Fast Movement: +33% melee, quicker on foot.");
                    if (BrutalCritTier(lvl) > 0) sb.AppendLine($"Brutal Critical {Roman(BrutalCritTier(lvl))}: heavier hits and armor-shredding.");
                    if (lvl >= 11) sb.AppendLine("Relentless Rage: while raging, far harder to put down.");
                    if (lvl >= 20) sb.AppendLine("Primal Champion: peak might - bonus melee and toughness.");
                    break;
                case "RH_Monk":
                    sb.AppendLine($"Martial Arts: unarmed strikes scale with level (+{Mathf.RoundToInt(0.6f * lvl)}% melee).");
                    sb.AppendLine("Ki: Flurry of Blows, Patient Defense, Step of the Wind.");
                    if (lvl >= 5) sb.AppendLine("Extra Attack + Stunning Strike.");
                    if (lvl >= 7) sb.AppendLine("Evasion: takes less damage from blasts.");
                    if (lvl >= 10) sb.AppendLine("Purity of Body: strong resistance to disease and toxins.");
                    if (lvl >= 14) sb.AppendLine("Diamond Soul: iron-willed against mental strain.");
                    if (lvl >= 18) sb.AppendLine("Empty Body: can slip out of sight.");
                    break;
                case "RH_Rogue":
                    sb.AppendLine($"Sneak Attack: +{Mathf.RoundToInt(5f * lvl)}% damage to distracted or unaware foes.");
                    sb.AppendLine("Cunning Action + Expertise: quick escapes, faster learning.");
                    if (lvl >= 5) sb.AppendLine("Uncanny Dodge: halves a telling blow.");
                    if (lvl >= 7) sb.AppendLine("Evasion: takes less damage from blasts.");
                    if (lvl >= 18) sb.AppendLine("Elusive: maddeningly hard to pin down.");
                    if (lvl >= 20) sb.AppendLine("Stroke of Luck: fortune turns misses into hits.");
                    break;
                case "RH_Ranger":
                    sb.AppendLine(h.favoredEnemy == FavoredEnemy.None
                        ? "Favored Enemy: not yet chosen."
                        : $"Favored Enemy ({h.favoredEnemy}): +{Mathf.RoundToInt((lvl >= 20 ? 6f : 4f) * lvl)}% damage to that foe.");
                    if (lvl >= 5) sb.AppendLine("Extra Attack: +33% weapon damage.");
                    if (lvl >= 8) sb.AppendLine("Land's Stride: surer footing, quicker travel.");
                    if (lvl >= 14) sb.AppendLine("Vanish: slip out of sight in a pinch.");
                    if (lvl >= 18) sb.AppendLine("Feral Senses: nothing escapes your aim.");
                    break;
                case "RH_Paladin":
                    sb.AppendLine("Fighting Style: Defense (armor). Divine Smite empowers your strikes.");
                    if (lvl >= 3) sb.AppendLine("Divine Health: immune to disease.");
                    if (lvl >= 5) sb.AppendLine("Extra Attack: +33% melee damage.");
                    if (lvl >= 6) sb.AppendLine($"Aura of Protection: nearby allies are tougher{(lvl >= 18 ? " (wide aura)" : "")}.");
                    if (lvl >= 10) sb.AppendLine("Aura of Courage: nearby allies resist fear and mental strain.");
                    if (lvl >= 11) sb.AppendLine("Improved Divine Smite: +radiant melee damage.");
                    if (lvl >= 14) sb.AppendLine("Cleansing Touch: strip afflictions from an ally.");
                    break;
                case "RH_Cleric":
                    sb.AppendLine("Divine conduit: your spells grow stronger as you rise.");
                    if (lvl >= 2) sb.AppendLine("Channel Divinity: Turn Undead and a burst of healing radiance.");
                    if (lvl >= 10) sb.AppendLine("Divine Intervention: a miracle that mends and cleanses your allies.");
                    break;
                case "RH_Druid":
                    sb.AppendLine("Nature's voice: stronger spells, faster plant work, surer footing.");
                    sb.AppendLine("Wild Shape: take the forms of beasts in battle.");
                    break;
                case "RH_Sorcerer":
                    sb.AppendLine("Font of Magic: raw innate power - your spells hit harder than a wizard's study allows.");
                    sb.AppendLine("Metamagic: choose arcane refinements as feats as you level.");
                    break;
                case "RH_Bard":
                    sb.AppendLine("Jack of All Trades: faster learning and a silver tongue.");
                    sb.AppendLine("Bardic Inspiration: embolden an ally.");
                    if (lvl >= 6) sb.AppendLine("Countercharm: nearby allies resist fear and despair.");
                    if (lvl >= 10) sb.AppendLine("Magical Secrets: a few spells borrowed from other traditions.");
                    break;
                case "RH_Warlock":
                    sb.AppendLine(h.pactBoon == PactBoon.None
                        ? "Pact Boon: not yet chosen."
                        : (h.pactBoon == PactBoon.Blade ? "Pact of the Blade: your strikes hit far harder." : "Pact of the Tome: deeper arcane power."));
                    sb.AppendLine("Eldritch Invocations: choose otherworldly gifts as feats as you level.");
                    break;
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
