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

        private enum ChoiceKind { Style, Favored, Pact, Secret, Trait, Feat, BonusFeat }

        // Bard Magical Secrets pool: spells borrowed from other traditions.
        private static readonly string[] SecretPool =
        {
            "RH_Spell_Fireball", "RH_Spell_LightningBolt", "RH_Spell_ConeOfCold", "RH_Spell_Heal",
            "RH_Spell_MassHeal", "RH_Spell_Haste", "RH_Spell_Revivify", "RH_Spell_HoldMonster",
            "RH_Spell_Disintegrate", "RH_Spell_Slow",
        };

        private class PendingChoice
        {
            public int level;
            public ChoiceKind kind;
        }

        public static void CheckLevelChoices(Hediff_HeroLevels hero, bool allowDialog = true)
        {
            if (hero?.pawn == null || hero.classDef == null) return;
            bool player = allowDialog && hero.pawn.IsColonistPlayerControlled;
            while (true)
            {
                var next = NextUnresolved(hero);
                if (next == null) return;
                var options = BuildOptions(hero, next.kind);
                if (!player || options.Count == 0)
                {
                    if (options.Count > 0) options.RandomElement().apply();
                    MarkResolved(hero, next);
                    continue;
                }
                var captured = next;
                Find.WindowStack.Add(new Dialog_HeroChoice(TitleFor(next.kind), SubtitleFor(hero, next.kind), options, () =>
                {
                    MarkResolved(hero, captured);
                    CheckLevelChoices(hero);
                }));
                return; // resolve the rest after this dialog closes
            }
        }

        private static PendingChoice NextUnresolved(Hediff_HeroLevels hero)
        {
            PendingChoice best = null;
            void consider(int lvl, ChoiceKind kind, bool resolved)
            {
                if (lvl <= hero.level && !resolved && (best == null || lvl < best.level))
                {
                    best = new PendingChoice { level = lvl, kind = kind };
                }
            }
            if (hero.classDef.fightingStyles)
            {
                consider(1, ChoiceKind.Style, hero.FightingStyle != FighterStyle.None);
            }
            if (hero.classDef.favoredEnemyPick)
            {
                consider(1, ChoiceKind.Favored, hero.favoredEnemy != FavoredEnemy.None);
            }
            if (hero.classDef.pactBoonPick)
            {
                consider(3, ChoiceKind.Pact, hero.pactBoon != PactBoon.None);
            }
            if (hero.classDef.magicalSecretLevels != null)
            {
                foreach (int lvl in hero.classDef.magicalSecretLevels) consider(lvl, ChoiceKind.Secret, hero.IsChoiceResolved(lvl));
            }
            foreach (int lvl in TraitLevels) consider(lvl, ChoiceKind.Trait, hero.IsChoiceResolved(lvl));
            foreach (int lvl in FeatLevels) consider(lvl, ChoiceKind.Feat, hero.IsChoiceResolved(lvl));
            if (hero.classDef.bonusFeatLevels != null)
            {
                foreach (int lvl in hero.classDef.bonusFeatLevels) consider(lvl, ChoiceKind.BonusFeat, hero.IsBonusFeatResolved(lvl));
            }
            return best;
        }

        private static void MarkResolved(Hediff_HeroLevels hero, PendingChoice c)
        {
            // Style/Favored/Pact resolution is detected by the stored field being set (by the option's apply).
            if (c.kind == ChoiceKind.BonusFeat) hero.MarkBonusFeatResolved(c.level);
            else if (c.kind == ChoiceKind.Trait || c.kind == ChoiceKind.Feat || c.kind == ChoiceKind.Secret) hero.MarkChoiceResolved(c.level);
        }

        private static List<HeroChoiceOption> BuildOptions(Hediff_HeroLevels hero, ChoiceKind kind)
        {
            switch (kind)
            {
                case ChoiceKind.Style: return BuildStyleOptions(hero);
                case ChoiceKind.Favored: return BuildFavoredOptions(hero);
                case ChoiceKind.Pact: return BuildPactOptions(hero);
                case ChoiceKind.Secret: return BuildSecretOptions(hero);
                case ChoiceKind.Trait: return BuildTraitOptions(hero);
                default: return BuildFeatOptions(hero); // Feat + BonusFeat
            }
        }

        private static string TitleFor(ChoiceKind kind)
        {
            switch (kind)
            {
                case ChoiceKind.Style: return "Choose a fighting style";
                case ChoiceKind.Favored: return "Choose your favored prey";
                case ChoiceKind.Pact: return "Choose your pact boon";
                case ChoiceKind.Secret: return "Choose a magical secret";
                case ChoiceKind.Trait: return "Choose a trait";
                default: return "Choose a feat";
            }
        }

        private static string SubtitleFor(Hediff_HeroLevels hero, ChoiceKind kind)
        {
            switch (kind)
            {
                case ChoiceKind.Style: return $"{hero.pawn.LabelShortCap} settles into a way of fighting.";
                case ChoiceKind.Favored: return $"{hero.pawn.LabelShortCap} marks the kind of foe they hunt best.";
                case ChoiceKind.Pact: return $"{hero.pawn.LabelShortCap}'s patron offers a gift.";
                case ChoiceKind.Secret: return $"{hero.pawn.LabelShortCap} masters a spell from another tradition.";
                case ChoiceKind.Trait: return $"{hero.pawn.LabelShortCap} has grown — pick one trait to keep.";
                default: return $"{hero.pawn.LabelShortCap} has earned a feat — pick one.";
            }
        }

        private static readonly (FavoredEnemy foe, string label, string desc)[] FavoredPool =
        {
            (FavoredEnemy.Beasts, "Beasts", "You hunt wild animals best: bonus damage against animals."),
            (FavoredEnemy.Mechanoids, "Mechanoids", "You know where machines break: bonus damage against mechanoids."),
            (FavoredEnemy.Humanlikes, "Raiders", "You read people in a fight: bonus damage against humanlikes."),
            (FavoredEnemy.Insects, "Insects", "You cull the swarms: bonus damage against insectoids."),
        };

        private static List<HeroChoiceOption> BuildFavoredOptions(Hediff_HeroLevels hero)
        {
            return FavoredPool.Select(f => new HeroChoiceOption
            {
                label = f.label,
                description = f.desc,
                icon = null,
                apply = () => hero.SetFavoredEnemy(f.foe)
            }).ToList();
        }

        private static readonly (PactBoon boon, string label, string desc)[] PactPool =
        {
            (PactBoon.Blade, "Pact of the Blade", "Your patron's power flows into your strikes: a large bonus to melee damage."),
            (PactBoon.Tome, "Pact of the Tome", "Forbidden knowledge deepens your power: a bonus to spell power."),
            (PactBoon.Chain, "Pact of the Chain", "Bind a fiendish familiar - a fire-flinging imp - to your service (Summon Imp)."),
        };

        private static List<HeroChoiceOption> BuildPactOptions(Hediff_HeroLevels hero)
        {
            return PactPool.Select(b => new HeroChoiceOption
            {
                label = b.label,
                description = b.desc,
                icon = null,
                apply = () =>
                {
                    hero.SetPactBoon(b.boon);
                    if (b.boon == PactBoon.Chain)
                    {
                        var summon = DefDatabase<AbilityDef>.GetNamedSilentFail("RH_Ability_SummonImp");
                        if (summon != null) hero.pawn.abilities?.GainAbility(summon);
                    }
                }
            }).ToList();
        }

        private static List<HeroChoiceOption> BuildSecretOptions(Hediff_HeroLevels hero)
        {
            var pawn = hero.pawn;
            var owned = pawn.abilities?.abilities;
            return SecretPool
                .Select(d => DefDatabase<AbilityDef>.GetNamedSilentFail(d))
                .Where(d => d != null && (owned == null || !owned.Any(a => a.def == d)))
                .InRandomOrder().Take(OptionsShown)
                .Select(d => new HeroChoiceOption
                {
                    label = d.LabelCap,
                    description = d.description,
                    icon = d.uiIcon,
                    apply = () => { pawn.abilities?.GainAbility(d); hero.Notify_SpellGranted(d); }
                }).ToList();
        }

        private static readonly (FighterStyle style, string label, string desc)[] StylePool =
        {
            (FighterStyle.Quickness, "Quickness", "Light and fast: +20% melee attack speed."),
            (FighterStyle.Strength, "Strength", "Heavy hits: +15% melee damage."),
            (FighterStyle.Cunning, "Cunning", "Find the gap: +20% armor penetration on melee hits."),
        };

        private static List<HeroChoiceOption> BuildStyleOptions(Hediff_HeroLevels hero)
        {
            return StylePool.Select(s => new HeroChoiceOption
            {
                label = s.label,
                description = s.desc,
                icon = null,
                apply = () => hero.SetFightingStyle(s.style)
            }).ToList();
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
