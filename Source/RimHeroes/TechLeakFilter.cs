using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The optional "tech leak-stopper" (mod setting, default on). RimHeroes is built for a low-tech
    /// fantasy world; broad world-tech restriction is delegated to the World Tech Level mod, but a few
    /// spacer-flavoured quest sites leak guns and drop-pod mercenaries in regardless of the faction
    /// roster. When the setting is on we zero those quests' rootSelectionWeight so the storyteller never
    /// rolls them (the same lever our own never-auto-rolled quests use); when off we restore the original
    /// weight. No Harmony, and any blacklisted def that isn't loaded is simply skipped, so this is a safe
    /// no-op in any modlist.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class TechLeakFilter
    {
        // Spacer / high-tech quest sites that break the medieval-fantasy tone. Resolved by name, so a
        // def whose DLC isn't active is ignored. Extend this list as other clear leaks are identified.
        private static readonly string[] BlacklistedQuests =
        {
            "OpportunitySite_AncientMercenaries", // Odyssey: a camp of gun-toting spacer mercenaries
        };

        private static readonly Dictionary<string, float> originalWeights = new Dictionary<string, float>();

        static TechLeakFilter() => Apply();

        public static void Apply()
        {
            bool on = RimHeroesMod.Settings?.techLeakStopper ?? true;
            foreach (var name in BlacklistedQuests)
            {
                var def = DefDatabase<QuestScriptDef>.GetNamedSilentFail(name);
                if (def == null) continue;
                if (!originalWeights.ContainsKey(name)) originalWeights[name] = def.rootSelectionWeight;
                def.rootSelectionWeight = on ? 0f : originalWeights[name];
            }
        }
    }
}
