using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    public class HeroChoiceOption
    {
        public string label;
        public string description;
        public Texture2D icon;
        public Action apply;
    }

    /// <summary>
    /// Level-up choices: a vanilla non-combat/non-movespeed trait pick at levels 3 and 6, and a feat
    /// pick at ASI levels 4/8/12/16/19. Player colonists pick from a dialog; other heroes (raids,
    /// debug) auto-pick. Each level grants 1 from 5 options.
    /// </summary>
    public static class HeroChoices
    {
        public static readonly int[] TraitLevels = { 3, 6 };
        public static readonly int[] FeatLevels = { 4, 8, 12, 16, 19 };
        private const int OptionsShown = 5;

        // Vanilla traits offered at L3/L6: deliberately non-combat and non-movespeed. (defName, degree)
        private static readonly (string defName, int degree)[] TraitPool =
        {
            ("Industriousness", 2), ("Kind", 0), ("NaturalMood", 2), ("Nerves", 2), ("Ascetic", 0),
            ("QuickSleeper", 0), ("NightOwl", 0), ("TooSmart", 1), ("GreatMemory", 0), ("Neat", 0),
        };

        public static void CheckLevelChoices(Hediff_HeroLevels hero, bool allowDialog = true)
        {
            if (hero?.pawn == null || hero.classDef == null) return;
            bool player = allowDialog && hero.pawn.IsColonistPlayerControlled;
            while (true)
            {
                int lvl = NextUnresolved(hero);
                if (lvl < 0) return;
                bool isTrait = TraitLevels.Contains(lvl);
                var options = isTrait ? BuildTraitOptions(hero) : BuildFeatOptions(hero);
                if (!player || options.Count == 0)
                {
                    if (options.Count > 0) options.RandomElement().apply();
                    hero.MarkChoiceResolved(lvl);
                    continue;
                }
                int captured = lvl;
                string title = isTrait ? "Choose a trait" : "Choose a feat";
                string sub = isTrait
                    ? $"{hero.pawn.LabelShortCap} has grown — pick one trait to keep."
                    : $"{hero.pawn.LabelShortCap} has earned a feat — pick one.";
                Find.WindowStack.Add(new Dialog_HeroChoice(title, sub, options, () =>
                {
                    hero.MarkChoiceResolved(captured);
                    CheckLevelChoices(hero);
                }));
                return; // resolve the rest after this dialog closes
            }
        }

        private static int NextUnresolved(Hediff_HeroLevels hero)
        {
            int best = -1;
            foreach (int lvl in TraitLevels.Concat(FeatLevels))
            {
                if (lvl <= hero.level && !hero.IsChoiceResolved(lvl) && (best < 0 || lvl < best))
                {
                    best = lvl;
                }
            }
            return best;
        }

        private static List<HeroChoiceOption> BuildTraitOptions(Hediff_HeroLevels hero)
        {
            var pawn = hero.pawn;
            var pool = TraitPool
                .Select(t => new { def = DefDatabase<TraitDef>.GetNamedSilentFail(t.defName), t.degree })
                .Where(x => x.def != null && (pawn.story?.traits == null || !pawn.story.traits.HasTrait(x.def)))
                .ToList();
            return pool.InRandomOrder().Take(OptionsShown).Select(x =>
            {
                var trait = new Trait(x.def, x.degree);
                return new HeroChoiceOption
                {
                    label = trait.LabelCap,
                    description = x.def.DataAtDegree(x.degree)?.description ?? trait.CurrentData?.description ?? "",
                    icon = null,
                    apply = () => pawn.story?.traits?.GainTrait(new Trait(x.def, x.degree))
                };
            }).ToList();
        }

        private static List<HeroChoiceOption> BuildFeatOptions(Hediff_HeroLevels hero)
        {
            var available = DefDatabase<FeatDef>.AllDefs
                .Where(f => !hero.TakenFeats.Contains(f)
                            && (f.classes == null || f.classes.Contains(hero.classDef.defName)))
                .ToList();
            return available.InRandomOrder().Take(OptionsShown).Select(f => new HeroChoiceOption
            {
                label = f.LabelCap,
                description = f.description,
                icon = null,
                apply = () => ApplyFeat(hero, f)
            }).ToList();
        }

        public static void ApplyFeat(Hediff_HeroLevels hero, FeatDef feat)
        {
            var pawn = hero.pawn;
            if (feat.appliedHediff != null && !pawn.health.hediffSet.HasHediff(feat.appliedHediff))
            {
                pawn.health.AddHediff(feat.appliedHediff);
            }
            if (feat.grantedAbility != null)
            {
                pawn.abilities?.GainAbility(feat.grantedAbility);
                hero.Notify_SpellGranted(feat.grantedAbility);
            }
            if (feat.grantedTrait != null && pawn.story?.traits != null && !pawn.story.traits.HasTrait(feat.grantedTrait))
            {
                pawn.story.traits.GainTrait(new Trait(feat.grantedTrait, feat.grantedTraitDegree));
            }
            hero.AddTakenFeat(feat);
        }
    }
}
