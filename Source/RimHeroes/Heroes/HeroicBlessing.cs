using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The Heroic Blessing: a one-use relic that opens the class-selection flow on its user. The
    /// transformation itself (wipe rolled traits, cure lasting afflictions, grant the Hero + class
    /// traits, drop a starter weapon) runs through Dialog_ChooseHeroClass -> HeroUtility.MakeHero.
    /// </summary>
    public class CompProperties_UseEffectHeroicBlessing : CompProperties_UseEffect
    {
        public CompProperties_UseEffectHeroicBlessing() => compClass = typeof(CompUseEffect_HeroicBlessing);
    }

    public class CompUseEffect_HeroicBlessing : CompUseEffect
    {
        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (HeroUtility.IsHero(p))
            {
                return "RH_AlreadyHero".Translate(p.LabelShortCap);
            }
            if (!p.RaceProps.Humanlike || p.story == null)
            {
                return false;
            }
            return base.CanBeUsedBy(p);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            Find.WindowStack.Add(new Dialog_ChooseHeroClass(usedBy));
        }
    }
}
