using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Experience candy: a one-use treat that grants a fixed lump of hero XP to whoever eats it. Only
    /// heroes can benefit (a non-hero would just be eating candy), and a maxed hero gains nothing.
    /// </summary>
    public class CompProperties_UseEffectGrantHeroXP : CompProperties_UseEffect
    {
        public float xp = 500f;
        public CompProperties_UseEffectGrantHeroXP() => compClass = typeof(CompUseEffect_GrantHeroXP);
    }

    public class CompUseEffect_GrantHeroXP : CompUseEffect
    {
        public new CompProperties_UseEffectGrantHeroXP Props => (CompProperties_UseEffectGrantHeroXP)props;

        public override AcceptanceReport CanBeUsedBy(Pawn p)
        {
            if (!HeroUtility.IsHero(p)) return "RH_CandyNotHero".Translate();
            var h = HeroUtility.GetHeroHediff(p);
            if (h != null && h.AtMaxLevel) return "RH_CandyMaxLevel".Translate(p.LabelShortCap);
            return base.CanBeUsedBy(p);
        }

        public override void DoEffect(Pawn usedBy)
        {
            base.DoEffect(usedBy);
            var h = HeroUtility.GetHeroHediff(usedBy);
            if (h == null) return;
            h.GainXP(Props.xp);
            Messages.Message("RH_CandyEaten".Translate(usedBy.LabelShortCap, Props.xp.ToString("F0")),
                usedBy, MessageTypeDefOf.PositiveEvent, false);
        }
    }
}
