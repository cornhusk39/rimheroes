using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace RimHeroes
{
    /// <summary>
    /// Sorcerer Metamagic - Quickened Spell: cut the cooldown of the sorcerer's spells. (Twinned is
    /// handled in the spell comps; the rest are class-feature stats.)
    /// </summary>
    [HarmonyPatch(typeof(Ability), nameof(Ability.StartCooldown))]
    public static class Patch_Quickened
    {
        public static void Prefix(Ability __instance, ref int ticks)
        {
            if (__instance is Ability_Spell && ClassFeatures.HasMetamagic(__instance.pawn, "RH_Feat_Quickened"))
            {
                ticks = Mathf.RoundToInt(ticks * 0.6f);
            }
        }
    }
}
