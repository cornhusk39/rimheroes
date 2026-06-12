using RimWorld;
using Verse;

namespace RimHeroes
{
    public static class HeroUtility
    {
        private static readonly string[] CorpseDestroyingRecipes = { "CremateCorpse", "ButcherCorpseFlesh" };

        public static bool CorpseBillBlocked(RecipeDef recipe, Thing thing)
        {
            if (RimHeroesMod.Settings.allowHeroCorpseDestruction || recipe == null)
            {
                return false;
            }
            return thing is Corpse corpse
                   && IsHero(corpse.InnerPawn)
                   && System.Array.IndexOf(CorpseDestroyingRecipes, recipe.defName) >= 0;
        }

        public static Hediff_HeroLevels GetHeroHediff(Pawn pawn) =>
            pawn?.health?.hediffSet?.GetFirstHediffOfDef(RH_DefOf.RH_HeroLevels) as Hediff_HeroLevels;

        public static bool IsHero(Pawn pawn) => GetHeroHediff(pawn) != null;

        /// <summary>
        /// The single entry point for making a pawn a Hero: adds the Hero trait, the class flavor
        /// trait, and the progression hediff at level 1. Used by the scenario, and later by Class
        /// Tomes/rituals.
        /// </summary>
        public static Hediff_HeroLevels MakeHero(Pawn pawn, HeroClassDef classDef)
        {
            if (pawn == null || classDef == null)
            {
                return null;
            }
            var existing = GetHeroHediff(pawn);
            if (existing != null)
            {
                return existing;
            }
            if (pawn.story?.traits != null && !pawn.story.traits.HasTrait(RH_DefOf.RH_Hero))
            {
                pawn.story.traits.GainTrait(new Trait(RH_DefOf.RH_Hero));
            }
            if (classDef.classTrait != null && pawn.story?.traits != null && !pawn.story.traits.HasTrait(classDef.classTrait))
            {
                pawn.story.traits.GainTrait(new Trait(classDef.classTrait));
            }
            var hediff = (Hediff_HeroLevels)HediffMaker.MakeHediff(RH_DefOf.RH_HeroLevels, pawn);
            hediff.classDef = classDef;
            pawn.health.AddHediff(hediff);
            if (classDef.vestmentHediff != null && !pawn.health.hediffSet.HasHediff(classDef.vestmentHediff))
            {
                var vestment = (Hediff_ClassVestment)pawn.health.AddHediff(classDef.vestmentHediff);
                vestment.SetTierForLevel(hediff.level);
            }
            return hediff;
        }
    }
}
