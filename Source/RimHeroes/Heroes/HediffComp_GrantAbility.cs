using RimWorld;
using Verse;

namespace RimHeroes
{
    public class HediffCompProperties_GrantAbility : HediffCompProperties
    {
        public AbilityDef ability;
        public HediffCompProperties_GrantAbility() => compClass = typeof(HediffComp_GrantAbility);
    }

    /// <summary>
    /// Grants an ability for as long as this hediff is present, then removes it. Used to give each
    /// wildshape form its own signature attack (only usable while in that form).
    /// </summary>
    public class HediffComp_GrantAbility : HediffComp
    {
        public HediffCompProperties_GrantAbility Props => (HediffCompProperties_GrantAbility)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (Props.ability != null) parent.pawn.abilities?.GainAbility(Props.ability);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            if (Props.ability != null) parent.pawn.abilities?.RemoveAbility(Props.ability);
        }
    }
}
