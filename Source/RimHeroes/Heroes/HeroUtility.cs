using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    public static class HeroUtility
    {
        private static readonly string[] CorpseDestroyingRecipes = { "CremateCorpse", "ButcherCorpseFlesh" };

        /// <summary>
        /// Tier suffix for class-weapon defNames. RimWorld forbids ThingDef defNames that end in a
        /// digit, so the five weapon tiers use Roman numerals (e.g. RH_Weapon_Rogue_TierIII).
        /// </summary>
        public static string WeaponTierSuffix(int tier)
        {
            switch (tier)
            {
                case 1: return "TierI";
                case 2: return "TierII";
                case 3: return "TierIII";
                case 4: return "TierIV";
                case 5: return "TierV";
                default: return "Tier" + tier;
            }
        }

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
            // The blessing remakes flesh and fate: strip rolled traits and lasting afflictions so the
            // hero begins clean. Universal - so enemy heroes generated for raids are clean too.
            CleanseForHeroism(pawn, classDef);
            if (pawn.story?.traits != null && !pawn.story.traits.HasTrait(RH_DefOf.RH_Hero))
            {
                pawn.story.traits.GainTrait(new Trait(RH_DefOf.RH_Hero));
            }
            if (classDef.classTrait != null && pawn.story?.traits != null && !pawn.story.traits.HasTrait(classDef.classTrait))
            {
                pawn.story.traits.GainTrait(new Trait(classDef.classTrait));
            }
            NormalizeHeroBody(pawn);
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

        /// <summary>
        /// Wipes the pawn's rolled traits and lasting afflictions (missing limbs, addictions, chronic
        /// illness, injuries) so a new hero begins whole. The Hero + class traits are added afterwards.
        /// </summary>
        public static void CleanseForHeroism(Pawn pawn, HeroClassDef classDef)
        {
            if (pawn.story?.traits != null && pawn.story.traits.allTraits.Count > 0)
            {
                pawn.story.traits.allTraits.Clear();
                pawn.Notify_DisabledWorkTypesChanged();
                pawn.skills?.Notify_SkillDisablesChanged();
            }
            if (pawn.health?.hediffSet != null)
            {
                foreach (var h in pawn.health.hediffSet.hediffs.Where(x => x.def.isBad).ToList())
                {
                    pawn.health.RemoveHediff(h);
                }
                pawn.health.Notify_HediffChanged(null);
            }
        }

        /// <summary>Drops a poor-quality Lesser (T1) class weapon at the new hero's feet (player path).</summary>
        public static void GrantStarterWeapon(Pawn pawn, HeroClassDef classDef)
        {
            if (pawn?.Map == null || classDef == null)
            {
                return;
            }
            var def = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Weapon_" + classDef.defName.Substring("RH_".Length) + "_" + WeaponTierSuffix(1));
            if (def == null)
            {
                return;
            }
            var w = (ThingWithComps)ThingMaker.MakeThing(def);
            w.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Poor, ArtGenerationContext.Colony);
            GenPlace.TryPlaceThing(w, pawn.Position, pawn.Map, ThingPlaceMode.Near);
        }

        /// <summary>
        /// Becoming a Hero settles the body into one of two heroic frames (standard male/female)
        /// so the vestment art always fits - no fat, hulk, or thin bodies on heroes.
        /// </summary>
        public static void NormalizeHeroBody(Pawn pawn)
        {
            if (pawn.story == null)
            {
                return;
            }
            var target = pawn.gender == Gender.Female ? BodyTypeDefOf.Female : BodyTypeDefOf.Male;
            if (pawn.story.bodyType != target)
            {
                pawn.story.bodyType = target;
                pawn.Drawer?.renderer?.SetAllGraphicsDirty();
            }
        }
    }
}
