using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A D&D class. One per Hero pawn, referenced from Hediff_HeroLevels.
    /// Field list modeled on RWoM's TM_CustomClassDef (the proven shape) + VPE path defs.
    /// </summary>
    public class HeroClassDef : Def
    {
        public TraitDef classTrait;          // flavor trait applied alongside RH_Hero
        public HediffDef vestmentHediff;     // the class vestment (persistent armor-as-hediff)
        public string iconPath;              // optional; falls back to the class's tier-1 vestment portrait

        private Texture2D iconCached;

        /// <summary>Class portrait for menus (class picker, hero tab). Uses iconPath if set, otherwise the
        /// tier-1 vestment's south-facing sprite, so every class shows its own look without extra art.</summary>
        public Texture2D Icon
        {
            get
            {
                if (iconCached == null)
                {
                    string path = !iconPath.NullOrEmpty()
                        ? iconPath
                        : "Things/RimHeroes/Vestments/" + GenText.CapitalizeFirst(label) + "/T1_Male_south";
                    iconCached = ContentFinder<Texture2D>.Get(path, false) ?? BaseContent.BadTex;
                }
                return iconCached;
            }
        }
        public int maxLevel = 20;
        public CasterProgression casterProgression = CasterProgression.None;
        // 5e prepared casters (Wizard/Cleric/Druid/Paladin) ready a subset of known leveled spells
        // and can swap them during a window after a long rest. Known casters cast all they know.
        public bool preparesSpells = false;

        // Wizard signature mechanics (gated by level in Hediff_HeroLevels):
        //   arcaneRecovery  - L1: a short rest also recovers a burst of expended spell slots.
        //   spellMastery    - L18: one chosen lvl-1 and one lvl-2 spell become at-will (no slot).
        //   signatureSpells - L20: two chosen lvl-3 spells can each be cast free once per long rest.
        public bool arcaneRecovery = false;
        public bool spellMastery = false;
        public bool signatureSpells = false;

        // Fighter's L1 Fighting Style pick (Quickness/Strength/Cunning).
        public bool fightingStyles = false;
        // Ranger's L1 Favored Enemy pick (Beasts/Mechanoids/Humanlikes/Insects).
        public bool favoredEnemyPick = false;
        // Warlock's L3 Pact Boon pick (Blade/Tome).
        public bool pactBoonPick = false;
        // Bard's Magical Secrets: levels at which they pick a spell from the cross-class pool.
        public List<int> magicalSecretLevels = new List<int>();

        // Known casters (preparesSpells=false) LEARN a limited set the player picks; prepared casters
        // (preparesSpells=true) know all their granted spells. spellsKnownByLevel = leveled spells
        // known at each class level (5e table, 20 entries); cantripsKnownBase = cantrips at level 1
        // (+1 at L4, +1 at L10). Leave empty for know-all (prepared) classes.
        public List<int> spellsKnownByLevel = new List<int>();
        public int cantripsKnownBase = 0;
        // Extra ASI levels granted as bonus feat picks beyond the shared 4/8/12/16/19 (Fighter: 6, 14).
        public List<int> bonusFeatLevels = new List<int>();

        // What each level grants: abilities (spells/features as AbilityDefs), passive feature hediffs.
        public List<HeroLevelGrant> levelGrants = new List<HeroLevelGrant>();

        // The fixed mim trio + the L20 free pick is handled in code (level 20 => choice UI).
        public List<MimUnlock> mimUnlocks = new List<MimUnlock>();

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (var e in base.ConfigErrors()) yield return e;
            if (maxLevel < 1 || maxLevel > 20) yield return $"{defName}: maxLevel {maxLevel} outside 1-20";
        }
    }

    public class HeroLevelGrant
    {
        public int level = 1;
        public List<AbilityDef> abilities;   // vanilla AbilityDefs for now; VEF.Abilities later if spell engine needs it
        public List<HediffDef> features;     // passive class features (Extra Attack, Evasion, ...)
    }

    public class MimUnlock
    {
        public int level = 3;
        public MimJobDef job;
    }
}
