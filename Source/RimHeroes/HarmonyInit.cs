using System.Linq;
using HarmonyLib;
using Verse;

namespace RimHeroes
{
    [StaticConstructorOnStartup]
    public static class HarmonyInit
    {
        public const string HarmonyId = "cornhusk39.rimheroes";

        static HarmonyInit()
        {
            var harmony = new Harmony(HarmonyId);
            harmony.PatchAll();
            Log.Message($"[RimHeroes] initialized ({harmony.GetPatchedMethods().Count()} patched methods)");
        }
    }
}
