using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Weapon row screenshots: -quicktest -rhweaponshot [-rhweaponclass=RH_Barbarian].
    /// Spawns five drafted heroes of one class at levels 1/5/10/15/20 (tiers T1-T5), each holding its
    /// tier weapon. Runs UNPAUSED for a few seconds first so the held-weapon draw meshes build (a pawn
    /// paused the instant it spawns renders weaponless), then pauses and shoots south, east, and north,
    /// logging each pawn's screen position, and exits.
    /// </summary>
    public class GameComponent_WeaponShotSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhweaponshot");

        private static readonly int[] Levels = { 1, 5, 10, 15, 20 };

        private int state;
        private float gate = -1f;
        private Pawn[] pawns;
        private IntVec3 anchor;

        public GameComponent_WeaponShotSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 6) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;

            switch (state)
            {
                case 0:
                {
                    string className = "RH_Barbarian";
                    GenCommandLine.TryGetCommandLineArg("rhweaponclass", out var cls);
                    if (!cls.NullOrEmpty()) className = cls;
                    var classDef = DefDatabase<HeroClassDef>.GetNamed(className);
                    string shortName = className.Replace("RH_", "");

                    var searchFrom = map.Center + new IntVec3(0, 0, -35);
                    CellFinder.TryFindRandomCellNear(searchFrom, map, 10, c => c.Standable(map), out anchor);
                    ClearAndSand(map, 14, 5);   // clean, uniform strip so pawns sit evenly on one background
                    pawns = new Pawn[Levels.Length];
                    for (int i = 0; i < Levels.Length; i++)
                    {
                        try
                        {
                            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                                fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true, fixedGender: Gender.Male));
                            pawns[i] = p;
                            GenSpawn.Spawn(p, anchor + new IntVec3(i * 4 - 8, 0, 0), map);
                            p.apparel?.DestroyAll();
                            if (p.story != null) p.story.bodyType = BodyTypeDefOf.Male;
                            var levels = HeroUtility.MakeHero(p, classDef);
                            levels.SetLevelDirect(Levels[i]);
                            int tier = i + 1;
                            string wdef = $"RH_Weapon_{shortName}_{HeroUtility.WeaponTierSuffix(tier)}";
                            if (!EquipOn(p, wdef)) Log.Warning($"[RimHeroes.WeaponShot] FAIL equip {wdef}");
                            p.Drawer?.renderer?.SetAllGraphicsDirty();
                            if (p.drafter != null) p.drafter.Drafted = true;
                            Log.Message($"[RimHeroes.WeaponShot] pawn {i} {shortName} T{tier} (level {Levels[i]}) ready");
                        }
                        catch (System.Exception e) { Log.Error($"[RimHeroes.WeaponShot] pawn {i} failed: {e}"); }
                    }
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 9f);
                    // Run for a few seconds so the held-weapon meshes build before we pause to shoot.
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    gate = now + 5f;
                    state = 1;
                    break;
                }
                case 1:
                    Find.TickManager.Pause();
                    Pin(Rot4.South);
                    gate = now + 1.5f; state = 2; break;
                case 2:
                    ScreenshotTaker.TakeNonSteamShot("rhweaponrow_south");
                    Pin(Rot4.East);
                    gate = now + 1.5f; state = 3; break;
                case 3:
                    ScreenshotTaker.TakeNonSteamShot("rhweaponrow_east");
                    Pin(Rot4.North);
                    gate = now + 1.5f; state = 4; break;
                case 4:
                    ScreenshotTaker.TakeNonSteamShot("rhweaponrow_north");
                    gate = now + 0.5f; state = 5; break;
                case 5:
                    Log.Message("[RimHeroes.WeaponShot] RESULT: weapon row shots taken verdict=PASS");
                    state = 6;
                    Root.Shutdown();
                    break;
            }
        }

        private void Pin(Rot4 rot)
        {
            var ws = Find.WindowStack;
            if (ws != null)
                foreach (var w in ws.Windows.ToList())
                {
                    var n = w.GetType().Name;
                    if (n.Contains("Log") || n.Contains("EditWindow")) w.Close(false);
                }
            Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 9f);
            foreach (var p in pawns)
            {
                if (p == null) continue;
                p.jobs?.StopAll();
                p.Rotation = rot;
            }
            for (int i = 0; i < pawns.Length; i++)
            {
                if (pawns[i] == null) continue;
                var sp = Find.Camera.WorldToScreenPoint(pawns[i].DrawPos);
                Log.Message($"[RimHeroes.WeaponShot] SCREENPOS {rot.ToStringHuman()} {i} {sp.x:F0} {Screen.height - sp.y:F0}");
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

        private bool EquipOn(Pawn p, string weaponDef)
        {
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDef);
                if (def == null) { Log.Error("[RimHeroes.WeaponShot] missing def " + weaponDef); return false; }
                var w = (ThingWithComps)ThingMaker.MakeThing(def);
                p.equipment.MakeRoomFor(w);
                p.equipment.AddEquipment(w);
                return true;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.WeaponShot] equip {weaponDef} failed: {e}"); return false; }
        }
    }
}
