using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Nudges the staff casters' held weapon off center-mass toward their right hand, so the staff
    /// reads as held rather than strapped to the chest. Facing-aware so it sits on the right hand in
    /// every rotation. Only the staff classes (wizard/sorcerer/cleric/druid) are shifted.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    public static class Patch_CasterWeaponOffset
    {
        private static readonly HashSet<string> ShiftClasses = new HashSet<string>
        {
            "RH_Wizard", "RH_Sorcerer", "RH_Cleric", "RH_Druid"
        };

        private const float Shift = 0.22f;

        // Dev toggle so the caster-hand spike can capture a before/after in one build.
        public static bool ShiftEnabled = true;

        public static void Prefix(Thing eq, ref Vector3 drawLoc)
        {
            if (!ShiftEnabled)
            {
                return;
            }
            var ext = eq?.def?.GetModExtension<WeaponLockExtension>();
            if (ext == null || ext.heroClass == null || !ShiftClasses.Contains(ext.heroClass))
            {
                return;
            }
            if (!((eq.ParentHolder as Pawn_EquipmentTracker)?.pawn is Pawn pawn))
            {
                return;
            }
            drawLoc += RightOf(pawn.Rotation) * Shift;
        }

        // The pawn's right-hand direction in world space for each facing.
        private static Vector3 RightOf(Rot4 rot)
        {
            switch (rot.AsInt)
            {
                case 0: return new Vector3(1f, 0f, 0f);    // North faces +z -> right is east (+x)
                case 1: return new Vector3(0f, 0f, -1f);   // East faces +x -> right is south (-z)
                case 2: return new Vector3(-1f, 0f, 0f);   // South faces -z -> right is west (-x)
                default: return new Vector3(0f, 0f, 1f);   // West faces -x -> right is north (+z)
            }
        }
    }
}
