using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Kill XP: scaled by the victim's combatPower (defeating strong pawns pays disproportionately).
    /// Friendly-fire pays nothing; wild animals (hunting) pay reduced XP.
    /// </summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_HeroXP
    {
        public static void Postfix(Pawn __instance, DamageInfo? dinfo)
        {
            if (!(dinfo?.Instigator is Pawn killer) || killer == __instance)
            {
                return;
            }
            var hediff = HeroUtility.GetHeroHediff(killer);
            if (hediff == null)
            {
                return;
            }
            float xp = Mathf.Max(5f, __instance.kindDef?.combatPower ?? 10f) * RH_Tuning.XPPerKillCombatPower;
            if (__instance.Faction == null)
            {
                xp *= RH_Tuning.WildKillXPFactor; // hunting
            }
            else if (!__instance.Faction.HostileTo(killer.Faction))
            {
                return; // no XP for killing friendlies/neutrals
            }
            hediff.GainXP(xp);
        }
    }

    /// <summary>
    /// Work XP: heroes earn a sliver of class XP from all skill learning (working, crafting,
    /// shooting practice...). One hook covers every work source in the game.
    /// </summary>
    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Learn))]
    public static class Patch_SkillRecord_Learn_HeroXP
    {
        public static void Postfix(SkillRecord __instance, float xp)
        {
            if (xp <= 0f)
            {
                return;
            }
            HeroUtility.GetHeroHediff(__instance.Pawn)?.GainXP(xp * RH_Tuning.XPPerSkillXP);
        }
    }
}
