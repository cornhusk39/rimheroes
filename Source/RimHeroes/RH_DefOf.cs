using RimWorld;
using Verse;

namespace RimHeroes
{
    [DefOf]
    public static class RH_DefOf
    {
        public static TraitDef RH_Hero;
        public static HediffDef RH_HeroLevels;

        static RH_DefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(RH_DefOf));
    }
}
