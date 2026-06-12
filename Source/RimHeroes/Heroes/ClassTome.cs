using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Class Tomes: the in-game path to heroism outside the scenario. Using one (neurotrainer
    /// pattern) makes the user a Hero of the tome's class via HeroUtility.MakeHero.
    /// </summary>
    public class CompProperties_UseEffectMakeHero : CompProperties_UseEffect
    {
        public HeroClassDef heroClass;

        public CompProperties_UseEffectMakeHero() => compClass = typeof(CompUseEffect_MakeHero);
    }

    public class CompUseEffect_MakeHero : CompUseEffect
    {
        public new CompProperties_UseEffectMakeHero Props => (CompProperties_UseEffectMakeHero)props;

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
            var hediff = HeroUtility.MakeHero(usedBy, Props.heroClass);
            if (hediff != null)
            {
                Find.LetterStack.ReceiveLetter("RH_TomeUsedLabel".Translate(usedBy.LabelShortCap),
                    "RH_TomeUsedText".Translate(usedBy.LabelShortCap, Props.heroClass.label),
                    LetterDefOf.PositiveEvent, usedBy);
            }
        }
    }
}
