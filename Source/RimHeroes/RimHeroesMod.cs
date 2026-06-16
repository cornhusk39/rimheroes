using UnityEngine;
using Verse;

namespace RimHeroes
{
    public class RimHeroesMod : Mod
    {
        public static RimHeroesSettings Settings;

        public RimHeroesMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<RimHeroesSettings>();
        }

        public override string SettingsCategory() => "RimHeroes";

        public override void WriteSettings()
        {
            base.WriteSettings();
            TechLeakFilter.Apply(); // re-apply the leak filter when the player toggles the setting
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            var listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled("RH_Settings_TechLeakStopper".Translate(), ref Settings.techLeakStopper,
                "RH_Settings_TechLeakStopper_Desc".Translate());
            listing.CheckboxLabeled("RH_Settings_EnemyHeroRaids".Translate(), ref Settings.enemyHeroRaids,
                "RH_Settings_EnemyHeroRaids_Desc".Translate());
            listing.CheckboxLabeled("RH_Settings_AllowHeroCorpseDestruction".Translate(), ref Settings.allowHeroCorpseDestruction,
                "RH_Settings_AllowHeroCorpseDestruction_Desc".Translate());
            listing.End();
        }
    }

    public class RimHeroesSettings : ModSettings
    {
        // When on, zeroes the selection weight of spacer-flavoured leak quests (e.g. the ancient gun
        // mercenaries) to protect the medieval tone. See TechLeakFilter. (DESIGN.md: anti-tech layer 1.)
        public bool techLeakStopper = true;
        // Rare Hero pawns in high-point raids, escorted by combat mims.
        public bool enemyHeroRaids = true;
        // By default Hero corpses are excluded from cremation/butcher bills so revival stays possible.
        public bool allowHeroCorpseDestruction = false;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref techLeakStopper, "techLeakStopper", true);
            Scribe_Values.Look(ref enemyHeroRaids, "enemyHeroRaids", true);
            Scribe_Values.Look(ref allowHeroCorpseDestruction, "allowHeroCorpseDestruction", false);
        }
    }
}
