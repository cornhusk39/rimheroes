using System.Collections.Generic;
using RimWorld;
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
        public int maxLevel = 20;
        public CasterProgression casterProgression = CasterProgression.None;

        // What each level grants: abilities (spells/features as AbilityDefs), passive feature hediffs.
        public List<HeroLevelGrant> levelGrants = new List<HeroLevelGrant>();

        // The fixed gestral trio + the L20 free pick is handled in code (level 20 => choice UI).
        public List<GestralUnlock> gestralUnlocks = new List<GestralUnlock>();

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

    public class GestralUnlock
    {
        public int level = 3;
        public GestralJobDef job;
    }
}
