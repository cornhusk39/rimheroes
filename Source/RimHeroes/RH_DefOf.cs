using RimWorld;
using Verse;

namespace RimHeroes
{
    [DefOf]
    public static class RH_DefOf
    {
        public static TraitDef RH_Hero;
        public static HediffDef RH_HeroLevels;
        public static HediffDef RH_DeathsDoor;
        public static HediffDef RH_Stabilized;
        public static HediffDef RH_MimDevotion;
        public static HediffDef RH_ClassFeatures;
        public static HediffDef RH_ActionSurge;
        public static HediffDef RH_Rage;
        public static HediffDef RH_Flurry;
        public static HediffDef RH_AuraProtection;
        public static HediffDef RH_AuraCourage;
        public static HediffDef RH_LongRestInterrupted;
        public static StatDef RH_SpellPower;

        static RH_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(RH_DefOf));
    }
}
