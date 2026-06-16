using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Dungeon layout: fills the pocket map with the kind's solid rock, then carves a handful of rooms
    /// joined by corridors (so the uncarved rock IS the dungeon walls). Marks the first room as the
    /// entrance (drops the climb-out exit there) and the last as the boss vault. Reads the active
    /// DungeonKind off the entrance portal to theme the rock/floor and records it for the populate step.
    /// </summary>
    public class GenStep_DungeonLayout : GenStep
    {
        private const int Margin = 4;
        private const int MinRoom = 8;
        private const int MaxRoom = 13;
        private const int RoomSpacing = 3;

        public override int SeedPart => 90210345;

        public override void Generate(Map map, GenStepParams parms)
        {
            var kind = Building_DungeonEntrance.GeneratingKind;

            var floor = kind?.floorDef
                        ?? DefDatabase<TerrainDef>.GetNamedSilentFail("AncientTile")
                        ?? TerrainDefOf.Soil;
            var rock = kind?.rockDef
                       ?? DefDatabase<ThingDef>.GetNamedSilentFail("Granite")
                       ?? DefDatabase<ThingDef>.AllDefs.FirstOrDefault(d => d.building != null && d.building.isNaturalRock && d.mineable);

            // Pick non-overlapping rooms.
            var rooms = new List<CellRect>();
            int target = Mathf.Clamp(map.Size.x / 14, 4, 7);
            for (int attempt = 0; attempt < 400 && rooms.Count < target; attempt++)
            {
                int w = Rand.RangeInclusive(MinRoom, MaxRoom);
                int h = Rand.RangeInclusive(MinRoom, MaxRoom);
                int x = Rand.RangeInclusive(Margin, map.Size.x - Margin - w);
                int z = Rand.RangeInclusive(Margin, map.Size.z - Margin - h);
                var r = new CellRect(x, z, w, h);
                if (rooms.Any(o => o.ExpandedBy(RoomSpacing).Overlaps(r))) continue;
                rooms.Add(r);
            }

            // Guarantee at least one room: a tiny or unlucky pocket map can wash out all 400 placement
            // attempts, and rooms[0] / the role indexing below must never index an empty list.
            if (rooms.Count == 0)
                rooms.Add(CellRect.CenteredOn(map.Center, MinRoom, MinRoom).ClipInsideMap(map));

            // Order rooms by distance from the first so corridors and roles read sensibly.
            var entrance = rooms[0];
            rooms = new List<CellRect> { entrance }
                .Concat(rooms.Skip(1).OrderBy(r => r.CenterCell.DistanceTo(entrance.CenterCell)))
                .ToList();

            // Compute the carved-out cells: room interiors + connecting corridors.
            var carved = new HashSet<IntVec3>();
            foreach (var room in rooms)
                foreach (var c in room.Cells)
                    carved.Add(c);
            for (int i = 1; i < rooms.Count; i++)
                CarveCorridor(rooms[i - 1].CenterCell, rooms[i].CenterCell, map, carved);

            // Lay terrain + roof everywhere; fill rock on every uncarved cell.
            foreach (var c in map.AllCells)
            {
                map.terrainGrid.SetTerrain(c, floor);
                map.roofGrid.SetRoof(c, RoofDefOf.RoofRockThick);
                if (!carved.Contains(c) && rock != null && c.GetEdifice(map) == null)
                {
                    GenSpawn.Spawn(ThingMaker.MakeThing(rock), c, map);
                }
            }

            // Drop the climb-out exit in the entrance room (only when entered through a portal).
            var portal = PocketMapUtility.currentlyGeneratingPortal;
            if (portal != null)
            {
                var exitDef = DefDatabase<ThingDef>.GetNamedSilentFail("CaveExit");
                if (exitDef != null)
                {
                    var spot = CellFinder.RandomClosewalkCellNear(entrance.CenterCell, map, 3, c => c.Standable(map) && c.GetEdifice(map) == null);
                    GenSpawn.Spawn(ThingMaker.MakeThing(exitDef), spot, map);
                }
            }
            MapGenerator.PlayerStartSpot = entrance.CenterCell;

            // Remember the layout + kind for the populate step and any runtime hooks.
            var comp = map.GetComponent<MapComponent_Dungeon>();
            if (comp != null)
            {
                comp.rooms = rooms;
                comp.entranceIndex = 0;
                comp.bossIndex = rooms.Count - 1;
                comp.kind = kind;
                var srcEntrance = Building_DungeonEntrance.Generating;
                comp.tier = srcEntrance?.tier ?? Building_DungeonEntrance.DebugForcedTier;
                comp.difficulty = srcEntrance?.difficulty ?? Building_DungeonEntrance.DebugForcedDifficulty;
                comp.capstoneWeapon = srcEntrance?.capstoneWeapon ?? Building_DungeonEntrance.DebugForcedCapstoneWeapon;
            }
        }

        // L-shaped corridor, two cells wide, carved into the rock set.
        private static void CarveCorridor(IntVec3 a, IntVec3 b, Map map, HashSet<IntVec3> carved)
        {
            int x = a.x, z = a.z;
            while (x != b.x) { x += x < b.x ? 1 : -1; AddWide(new IntVec3(x, 0, z), map, carved); }
            while (z != b.z) { z += z < b.z ? 1 : -1; AddWide(new IntVec3(x, 0, z), map, carved); }
        }

        private static void AddWide(IntVec3 c, Map map, HashSet<IntVec3> carved)
        {
            for (int dx = 0; dx <= 1; dx++)
                for (int dz = 0; dz <= 1; dz++)
                {
                    var cell = new IntVec3(c.x + dx, 0, c.z + dz);
                    if (cell.InBounds(map)) carved.Add(cell);
                }
        }
    }
}
