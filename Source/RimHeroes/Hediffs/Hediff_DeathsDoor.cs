using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// D&D death saving throws. Added by Hediff_HeroLevels when the hero would be dead but for
    /// their destiny (preventsDeath). Rolls a d20 each in-game hour: nat 20 = miraculous recovery,
    /// nat 1 = two failures, 10+ (with tend bonus) = success. Three successes = stabilized;
    /// three failures = death for real. Taking damage while dying = automatic failure (5e).
    /// </summary>
    public class Hediff_DeathsDoor : HediffWithComps
    {
        public int successes;
        public int failures;
        private int ticksUntilRoll = RollIntervalTicks;

        public const int RollIntervalTicks = 2500; // one in-game hour
        public const int CountNeeded = 3;
        public const int TendBonus = 3;
        public const int SaveDC = 10;

        public override string LabelInBrackets => $"{successes}/{CountNeeded} saved, {failures}/{CountNeeded} failed";

        public override bool ShouldRemove => false;

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            ticksUntilRoll -= delta;
            if (ticksUntilRoll <= 0)
            {
                ticksUntilRoll += RollIntervalTicks;
                ResolveRoll(Rand.RangeInclusive(1, 20));
            }
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (totalDamageDealt > 0f && dinfo.Def.ExternalViolenceFor(pawn))
            {
                Messages.Message("RH_DeathsDoorDamageFail".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent);
                Fail(1);
            }
        }

        /// <summary>Public so tests can force deterministic rolls.</summary>
        public void ResolveRoll(int roll)
        {
            if (pawn.Dead)
            {
                return;
            }
            if (roll == 20)
            {
                MiraculousRecovery();
                return;
            }
            if (roll == 1)
            {
                Messages.Message("RH_DeathsDoorCritFail".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent);
                Fail(2);
                return;
            }
            int modified = roll + (HasTendedInjury() ? TendBonus : 0);
            if (modified >= SaveDC)
            {
                successes++;
                Messages.Message("RH_DeathsDoorSave".Translate(pawn.LabelShortCap, successes, CountNeeded), pawn, MessageTypeDefOf.NeutralEvent);
                if (successes >= CountNeeded)
                {
                    Stabilize();
                }
            }
            else
            {
                Fail(1);
            }
        }

        private bool HasTendedInjury()
        {
            foreach (var hediff in pawn.health.hediffSet.hediffs)
            {
                if (hediff is HediffWithComps hwc && hwc.TryGetComp<HediffComp_TendDuration>()?.IsTended == true)
                {
                    return true;
                }
            }
            return false;
        }

        private void Fail(int count)
        {
            failures += count;
            if (failures >= CountNeeded)
            {
                DieForReal();
            }
            else
            {
                Messages.Message("RH_DeathsDoorFail".Translate(pawn.LabelShortCap, failures, CountNeeded), pawn, MessageTypeDefOf.NegativeEvent);
            }
        }

        private void Stabilize()
        {
            var p = pawn;
            p.health.AddHediff(RH_DefOf.RH_Stabilized);
            Find.LetterStack.ReceiveLetter("RH_StabilizedLabel".Translate(p.LabelShortCap),
                "RH_StabilizedText".Translate(p.LabelShortCap), LetterDefOf.PositiveEvent, p);
            p.health.RemoveHediff(this);
        }

        private void MiraculousRecovery()
        {
            var p = pawn;
            // "Back up with 1 HP": fully tend everything untended and fix the worst condition.
            foreach (var hediff in p.health.hediffSet.hediffs.ToArray())
            {
                if (hediff is HediffWithComps hwc)
                {
                    var tend = hwc.TryGetComp<HediffComp_TendDuration>();
                    if (tend != null && !tend.IsTended)
                    {
                        hediff.Tended(1f, 1f);
                    }
                }
            }
            HealthUtility.FixWorstHealthCondition(p);
            p.health.AddHediff(RH_DefOf.RH_Stabilized);
            Find.LetterStack.ReceiveLetter("RH_NatTwentyLabel".Translate(p.LabelShortCap),
                "RH_NatTwentyText".Translate(p.LabelShortCap), LetterDefOf.PositiveEvent, p);
            p.health.RemoveHediff(this);
        }

        private void DieForReal()
        {
            var p = pawn;
            Messages.Message("RH_DeathsDoorDeath".Translate(p.LabelShortCap), p, MessageTypeDefOf.PawnDeath);
            p.health.RemoveHediff(this); // clear before Kill so preventsDeath bookkeeping is clean
            HeroUtility.GetHeroHediff(p)?.Notify_FailedDeathSaves();
            p.Kill(null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref successes, "successes");
            Scribe_Values.Look(ref failures, "failures");
            Scribe_Values.Look(ref ticksUntilRoll, "ticksUntilRoll", RollIntervalTicks);
        }
    }
}
