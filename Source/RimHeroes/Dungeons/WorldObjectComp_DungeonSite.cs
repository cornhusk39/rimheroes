using RimWorld;
using RimWorld.Planet;
using Verse;

namespace RimHeroes
{
    /// <summary>Hangs the chosen DungeonKind + tier/difficulty off a world-map dungeon site, so the
    /// site-map GenStep knows what entrance + guards to build when the party visits.</summary>
    public class WorldObjectCompProperties_DungeonSite : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_DungeonSite() => compClass = typeof(WorldObjectComp_DungeonSite);
    }

    public class WorldObjectComp_DungeonSite : WorldObjectComp
    {
        public DungeonKindDef kind;
        public int tier = 1;
        public float difficulty = 1f;
        public ThingDef capstoneWeapon;   // when set, the dungeon's vault yields this weapon (Legendary)

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref kind, "rhKind");
            Scribe_Values.Look(ref tier, "rhTier", 1);
            Scribe_Values.Look(ref difficulty, "rhDifficulty", 1f);
            Scribe_Defs.Look(ref capstoneWeapon, "rhCapstoneWeapon");
        }
    }
}
