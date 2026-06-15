using HarmonyLib;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Marks a hero weapon as bound to one class and a minimum hero level. Only a hero pawn of that
    /// class, at or above the level, may equip it - no other classes, no non-heroes.
    /// </summary>
    public class WeaponLockExtension : DefModExtension
    {
        public string heroClass;   // HeroClassDef defName, e.g. "RH_Barbarian"
        public int minLevel = 1;   // 1 / 5 / 10 / 15 / 20 for the five rungs
        public int tier = 1;       // 1-5, for art/label bookkeeping
    }

    /// <summary>Spell magnitude multiplier read off the caster (raised by a class focus weapon).</summary>
    public static class SpellPower
    {
        public static float For(Pawn caster) => caster == null ? 1f : caster.GetStatValue(RH_DefOf.RH_SpellPower);
    }

    /// <summary>
    /// Class + level lock on hero weapons. Postfix on EquipmentUtility.CanEquip so the standard
    /// "can't equip" UI/AI paths all respect it.
    /// </summary>
    [HarmonyPatch(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
        new[] { typeof(Thing), typeof(Pawn), typeof(string), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    public static class Patch_EquipmentUtility_CanEquip_HeroLock
    {
        public static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (!__result || thing?.def == null || pawn == null)
            {
                return;
            }
            var ext = thing.def.GetModExtension<WeaponLockExtension>();
            if (ext == null)
            {
                return;
            }
            var hero = HeroUtility.GetHeroHediff(pawn);
            if (hero?.classDef == null || hero.classDef.defName != ext.heroClass)
            {
                __result = false;
                cantReason = "Only a hero of the right class can wield this";
                return;
            }
            if (hero.level < ext.minLevel)
            {
                __result = false;
                cantReason = $"Requires class level {ext.minLevel}";
            }
        }
    }
}
