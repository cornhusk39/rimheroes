using System.Collections.Generic;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Holds a generated dungeon's layout (room rects + which is the entrance / boss vault) and the
    /// DungeonKind it was built from, so the populate step and any runtime hooks can theme themselves
    /// without re-deriving the structure. Lightweight; only dungeon pocket maps ever fill it.
    /// </summary>
    public class MapComponent_Dungeon : MapComponent
    {
        public List<CellRect> rooms = new List<CellRect>();
        public int entranceIndex = -1;
        public int bossIndex = -1;
        public DungeonKindDef kind;
        public int tier = 1;
        public float difficulty = 1f;
        public ThingDef capstoneWeapon;

        public MapComponent_Dungeon(Map map) : base(map) { }

        public bool IsDungeon => rooms != null && rooms.Count > 0;
        public CellRect? BossRoom => (bossIndex >= 0 && bossIndex < rooms.Count) ? rooms[bossIndex] : (CellRect?)null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref rooms, "rooms", LookMode.Value);
            Scribe_Values.Look(ref entranceIndex, "entranceIndex", -1);
            Scribe_Values.Look(ref bossIndex, "bossIndex", -1);
            Scribe_Defs.Look(ref kind, "kind");
            Scribe_Values.Look(ref tier, "tier", 1);
            Scribe_Values.Look(ref difficulty, "difficulty", 1f);
            Scribe_Defs.Look(ref capstoneWeapon, "capstoneWeapon");
        }
    }
}
