using System.Collections.Generic;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Holds the generated crypt layout (room rects + which is the entrance / boss vault) so later
    /// phases can populate monsters, loot, and traps without re-deriving the structure. Lightweight;
    /// only dungeon pocket maps ever fill it.
    /// </summary>
    public class MapComponent_CryptDungeon : MapComponent
    {
        public List<CellRect> rooms = new List<CellRect>();
        public int entranceIndex = -1;
        public int bossIndex = -1;

        public MapComponent_CryptDungeon(Map map) : base(map) { }

        public bool IsDungeon => rooms != null && rooms.Count > 0;
        public CellRect? BossRoom => (bossIndex >= 0 && bossIndex < rooms.Count) ? rooms[bossIndex] : (CellRect?)null;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref rooms, "rooms", LookMode.Value);
            Scribe_Values.Look(ref entranceIndex, "entranceIndex", -1);
            Scribe_Values.Look(ref bossIndex, "bossIndex", -1);
        }
    }
}
