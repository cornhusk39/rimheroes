using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Weapon inspection sandbox: -quicktest -rhweapongrid. Spawns a 5x12 grid of drafted heroes
    /// (vestments on, each named "Class T#") on cleared sand: 12 columns (one per class) x 5 rows
    /// (T1 at top down to T5), each holding its tier weapon. Frames the camera on the grid and pauses,
    /// then leaves the game running for hands-on inspection. Does NOT screenshot or exit.
    /// </summary>
    public class GameComponent_WeaponGridSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhweapongrid");

        private static readonly string[] Classes =
        {
            "Barbarian", "Fighter", "Rogue", "Monk", "Ranger", "Paladin",
            "Wizard", "Sorcerer", "Cleric", "Druid", "Bard", "Warlock"
        };

        private const int ColGap = 3;
        private const int RowGap = 3;
        private const int XOffset = 16;
        private static readonly int[] TierLevels = { 1, 5, 10, 15, 20 };

        private bool done;
        private float spawnTime = -1f;
        private readonly List<Pawn> pawns = new List<Pawn>();
        private IntVec3 anchor;

        public GameComponent_WeaponGridSpike(Game game) { }

        private int HalfH => (TierLevels.Length - 1) * RowGap / 2;

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (spawnTime < 0f) { spawnTime = now + 3f; return; }   // let the map settle
            if (now < spawnTime) return;

            SpawnGrid(map);

            // face camera, pause, tidy up the message spam, then hand control to the player
            foreach (var p in pawns)
            {
                if (p == null) continue;
                p.jobs?.StopAll();
                p.Rotation = Rot4.South;
            }
            Find.LetterStack?.LettersListForReading?.Clear();
            Messages.Clear();
            Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 13f);
            // leave the game running (drafted pawns hold position): held-weapon draw meshes only
            // rebuild on a tick, so a spawn that pauses immediately renders the pawns weaponless.
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;

            Log.Message($"[RimHeroes.WeaponGrid] spawned {pawns.Count} heroes; inspect freely (running so weapons render; no auto-exit)");
            done = true;
        }

        private void SpawnGrid(Map map)
        {
            var searchFrom = map.Center + new IntVec3(0, 0, -10);
            CellFinder.TryFindRandomCellNear(searchFrom, map, 8, c => c.Standable(map), out anchor);
            ClearAndSand(map, XOffset + 8, HalfH + 8);

            for (int col = 0; col < Classes.Length; col++)
            {
                for (int row = 0; row < TierLevels.Length; row++)
                {
                    int tier = row + 1;
                    int x = col * ColGap - XOffset;
                    int z = HalfH - row * RowGap;            // T1 (row 0) highest on screen
                    var cell = anchor + new IntVec3(x, 0, z);
                    var p = SpawnHero(map, cell, "RH_" + Classes[col], TierLevels[row]);
                    if (p == null) continue;
                    p.Name = new NameSingle($"{Classes[col]} T{tier}");
                    pawns.Add(p);
                    string wdef = $"RH_Weapon_{Classes[col]}_T{tier}";
                    if (!EquipOn(p, wdef)) Log.Message($"[RimHeroes.WeaponGrid] FAIL equip {wdef}");
                }
            }
        }

        private void ClearAndSand(Map map, int halfW, int halfH)
        {
            for (int dx = -halfW; dx <= halfW; dx++)
                for (int dz = -halfH; dz <= halfH; dz++)
                {
                    var c = anchor + new IntVec3(dx, 0, dz);
                    if (!c.InBounds(map)) continue;
                    foreach (var t in c.GetThingList(map).ToList())
                    {
                        if (t is Pawn) continue;
                        if (t.def != null && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                    }
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Sand);
                    map.snowGrid?.SetDepth(c, 0f);
                    map.roofGrid?.SetRoof(c, null);
                }
        }

        private Pawn SpawnHero(Map map, IntVec3 cell, string cls, int level)
        {
            try
            {
                var classDef = DefDatabase<HeroClassDef>.GetNamedSilentFail(cls);
                if (classDef == null) { Log.Error("[RimHeroes.WeaponGrid] missing class " + cls); return null; }
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                    fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true, fixedGender: Gender.Male));
                GenSpawn.Spawn(p, cell, map);
                if (p.story != null) p.story.bodyType = BodyTypeDefOf.Male;
                p.apparel?.DestroyAll();                     // drop rolled civilian clothes; vestment shows once granted
                var levels = HeroUtility.MakeHero(p, classDef);
                levels.SetLevelDirect(level);
                p.Drawer?.renderer?.SetAllGraphicsDirty();
                if (p.drafter != null) p.drafter.Drafted = true;
                return p;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.WeaponGrid] spawn {cls} failed: {e}"); return null; }
        }

        private bool EquipOn(Pawn p, string weaponDef)
        {
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDef);
                if (def == null) { Log.Error("[RimHeroes.WeaponGrid] missing def " + weaponDef); return false; }
                var w = (ThingWithComps)ThingMaker.MakeThing(def);
                p.equipment.MakeRoomFor(w);
                p.equipment.AddEquipment(w);
                return true;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.WeaponGrid] equip {weaponDef} failed: {e}"); return false; }
        }
    }
}
