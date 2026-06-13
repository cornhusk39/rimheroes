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
        public static StatDef RH_SpellPower;

        static RH_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(RH_DefOf));
    }
}
