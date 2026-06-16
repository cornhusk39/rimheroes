using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The capstone payoff. When a hero's class earns its trial, this marks one of the two capstone-tier
    /// dungeons (Warren of Eyes or Titan's Forge, chosen at random) on the world map, carrying the
    /// hero's class capstone weapon so its vault yields it (Legendary). The stranger quest calls this on
    /// completion; the weapon is the class's tier-5 weapon.
    /// </summary>
    public static class CapstoneQuest
    {
        private static readonly string[] CapstoneKinds = { "RH_Dungeon_HundredEyes", "RH_Dungeon_AnnihilatorForge" };

        public static ThingDef CapstoneWeaponFor(HeroClassDef heroClass)
        {
            if (heroClass == null || !heroClass.defName.StartsWith("RH_")) return null;
            return DefDatabase<ThingDef>.GetNamedSilentFail("RH_Weapon_" + heroClass.defName.Substring(3) + "_T5");
        }

        public static WorldObject MarkCapstoneDungeon(HeroClassDef heroClass)
        {
            var weapon = CapstoneWeaponFor(heroClass);
            var kind = DefDatabase<DungeonKindDef>.GetNamedSilentFail(CapstoneKinds[Rand.Range(0, CapstoneKinds.Length)]);
            var part = DefDatabase<SitePartDef>.GetNamedSilentFail("RH_DungeonSite");
            if (kind == null || part == null) return null;
            if (!TileFinder.TryFindNewSiteTile(out var tile)) return null;

            var site = SiteMaker.MakeSite(part, tile, null, true);
            if (site == null) return null;
            var comp = site.GetComponent<WorldObjectComp_DungeonSite>();
            if (comp != null)
            {
                comp.kind = kind;
                comp.tier = 3;
                comp.difficulty = 2.4f;
                comp.capstoneWeapon = weapon;
            }
            Find.WorldObjects.Add(site);

            string weaponLabel = weapon != null ? weapon.label : "a legendary weapon";
            Find.LetterStack.ReceiveLetter("RH_CapstoneSiteLabel".Translate(),
                "RH_CapstoneSiteText".Translate(kind.LabelCap, weaponLabel),
                LetterDefOf.PositiveEvent, site);
            return site;
        }
    }
}
