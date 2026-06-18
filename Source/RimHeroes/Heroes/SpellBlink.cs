using RimWorld;
using Verse;
using Verse.Sound;

namespace RimHeroes
{
    /// <summary>Misty Step and the like: teleport the CASTER to the chosen cell. Vanilla's teleport effect
    /// moves a targeted thing, which does nothing when the target is empty ground, so casters need this.</summary>
    public class CompProperties_AbilityBlink : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityBlink() => compClass = typeof(CompAbilityEffect_Blink);
    }

    public class CompAbilityEffect_Blink : CompAbilityEffect
    {
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var caster = parent.pawn;
            var map = caster?.Map;
            if (map == null || !target.Cell.IsValid)
            {
                return;
            }
            IntVec3 to = target.Cell;
            if (!to.InBounds(map) || !to.Standable(map) || to.Fogged(map))
            {
                to = CellFinder.StandableCellNear(target.Cell, map, 4f);
            }
            if (!to.IsValid)
            {
                return;
            }
            IntVec3 from = caster.Position;
            FleckMaker.Static(from.ToVector3Shifted(), map, FleckDefOf.PsycastSkipFlashEntry, 2f);
            caster.Position = to;
            caster.Notify_Teleported(true, true);
            caster.stances?.SetStance(new Stance_Mobile());
            FleckMaker.Static(to.ToVector3Shifted(), map, FleckDefOf.PsycastSkipInnerExit, 2f);
            SoundDefOf.Psycast_Skip_Exit.PlayOneShot(new TargetInfo(to, map));
        }
    }
}
