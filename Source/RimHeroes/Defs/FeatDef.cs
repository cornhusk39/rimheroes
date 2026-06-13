using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A feat: a permanent perk a hero picks at an ASI level (4/8/12/16/19). RimWorld-flavored
    /// translation of 5e feats — applies a perk hediff, optionally grants an ability and/or a trait.
    /// </summary>
    public class FeatDef : Def
    {
        public HediffDef appliedHediff;       // the perk (stat offsets/factors)
        public AbilityDef grantedAbility;     // optional bonus spell/ability
        public TraitDef grantedTrait;         // optional vanilla trait grant
        public int grantedTraitDegree = 0;
        public List<string> classes;          // restrict to these HeroClassDef defNames (null = any)
    }
}
