using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    public enum RollAdvantage { None, Advantage, Disadvantage }

    public enum RollBucket { CriticalFailure, Failure, Success, CriticalSuccess }

    // What a check draws its base modifier from. Skill uses CheckDef.skill; the rest map to vanilla
    // skills/stats with no new game stats (see CheckMods).
    public enum CheckKind { Skill, Lockpick, Arcane, Perception, Dodge, Strength, Social, Endurance }

    public struct RollResult
    {
        public int rawDie;     // the natural d20 (1-20), before the modifier
        public int modifier;
        public int total;      // rawDie + modifier
        public int dc;
        public RollBucket bucket;

        public int Margin => total - dc;
        public bool Passed => bucket == RollBucket.Success || bucket == RollBucket.CriticalSuccess;
    }

    /// <summary>
    /// The d20 roll primitive: d20 + modifier vs DC, with advantage/disadvantage and natural 1/20
    /// crits. The roll itself uses the seeded game RNG (Verse.Rand); the cosmetic spin in the dialog
    /// uses UnityEngine.Random so it never disturbs game determinism.
    /// </summary>
    public static class D20
    {
        /// <summary>5e-style modifier from a hero: floor((skillLevel - 8) / 2), centered so RimWorld's
        /// 0-20 skills map to roughly -4..+6, plus any flat bonus on the check.</summary>
        public static int GetModifier(Pawn pawn, CheckDef check) => CheckMods.GetModifier(pawn, check);

        public static RollResult Roll(int modifier, int dc, RollAdvantage adv = RollAdvantage.None, int forcedRaw = 0)
        {
            int raw;
            if (forcedRaw >= 1 && forcedRaw <= 20)
            {
                raw = forcedRaw;
            }
            else if (adv == RollAdvantage.Advantage)
            {
                raw = Mathf.Max(Rand.RangeInclusive(1, 20), Rand.RangeInclusive(1, 20));
            }
            else if (adv == RollAdvantage.Disadvantage)
            {
                raw = Mathf.Min(Rand.RangeInclusive(1, 20), Rand.RangeInclusive(1, 20));
            }
            else
            {
                raw = Rand.RangeInclusive(1, 20);
            }

            int total = raw + modifier;
            RollBucket bucket;
            if (raw == 20) bucket = RollBucket.CriticalSuccess;
            else if (raw == 1) bucket = RollBucket.CriticalFailure;
            else if (total >= dc) bucket = RollBucket.Success;
            else bucket = RollBucket.Failure;

            return new RollResult { rawDie = raw, modifier = modifier, total = total, dc = dc, bucket = bucket };
        }

        /// <summary>Resolve a CheckDef for a pawn: rolls, then opens the animated dialog and calls back
        /// with the result when the player acknowledges it.</summary>
        public static void Resolve(Pawn pawn, CheckDef check, Action<RollResult> onResolved,
                                   RollAdvantage adv = RollAdvantage.None)
        {
            int mod = GetModifier(pawn, check);
            RollResult result = Roll(mod, check?.dc ?? 10, adv);
            Find.WindowStack.Add(new Dialog_D20Roll(pawn, check, result, onResolved));
        }
    }
}
