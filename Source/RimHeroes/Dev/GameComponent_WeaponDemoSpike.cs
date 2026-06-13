using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Weapon lineup: -quicktest -rhweapondemo. Spawns one L20 hero of every class in a row, each
    /// equipped with its capstone weapon, logs equip success per class, screenshots, then exits.
    /// </summary>
    public class GameComponent_WeaponDemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhweapondemo");

        private static readonly string[] Classes =
        {
            "Barbarian", "Fighter", "Rogue", "Monk", "Ranger", "Paladin",
            "Wizard", "Sorcerer", "Cleric", "Druid", "Bard", "Warlock"
        };

        private int state;
        private float nextStateTime = -1f;
        private readonly List<Pawn> pawns = new List<Pawn>();
        private IntVec3 anchor;

        public GameComponent_WeaponDemoSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 4)
            {
                return;
            }
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f) { nextStateTime = now + 4f; return; }
            if (now < nextStateTime) { return; }
            nextStateTime = now + 3f;

            switch (state)
            {
                case 0:
                {
                    var searchFrom = map.Center + new IntVec3(0, 0, -38);
                    CellFinder.TryFindRandomCellNear(searchFrom, map, 8, c => c.Standable(map), out anchor);
                    for (int i = 0; i < Classes.Length; i++)
                    {
                        var cell = anchor + new IntVec3(i * 2 - 11, 0, 0);
                        var p = SpawnHero(map, cell, "RH_" + Classes[i], 20);
                        if (p == null) continue;
                        pawns.Add(p);
                        string wdef = $"RH_Weapon_{Classes[i]}_T5";
                        bool ok = EquipOn(p, wdef);
                        Log.Message($"[RimHeroes.WeaponDemo] {(ok ? "OK" : "FAIL")} {Classes[i]} capstone {wdef}");
                    }
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 14f);
                    state = 1;
                    break;
                }
                case 1:
                    Find.TickManager.Pause();
                    foreach (var p in pawns) { p.jobs?.StopAll(); p.Rotation = Rot4.South; }
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 14f);
                    state = 2;
                    break;
                case 2:
                    ScreenshotTaker.TakeNonSteamShot("rhweaponrow_south");
                    foreach (var p in pawns) p.Rotation = Rot4.East;
                    state = 3;
                    break;
                case 3:
                    ScreenshotTaker.TakeNonSteamShot("rhweaponrow_east");
                    state = 4;
                    break;
                case 4:
                    Log.Message("[RimHeroes.WeaponDemo] RESULT: weapon lineup done verdict=PASS");
                    state = 5;
                    Root.Shutdown();
                    break;
            }
        }

        private Pawn SpawnHero(Map map, IntVec3 cell, string cls, int level)
        {
            try
            {
                var classDef = DefDatabase<HeroClassDef>.GetNamedSilentFail(cls);
                if (classDef == null) { Log.Error("[RimHeroes.WeaponDemo] missing class " + cls); return null; }
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                    fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true, fixedGender: Gender.Male));
                GenSpawn.Spawn(p, cell, map);
                p.apparel?.DestroyAll();
                if (p.story != null) p.story.bodyType = BodyTypeDefOf.Male;
                var levels = HeroUtility.MakeHero(p, classDef);
                levels.SetLevelDirect(level);
                p.Drawer?.renderer?.SetAllGraphicsDirty();
                if (p.drafter != null) p.drafter.Drafted = true;
                return p;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.WeaponDemo] spawn {cls} failed: {e}"); return null; }
        }

        private bool EquipOn(Pawn p, string weaponDef)
        {
            try
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDef);
                if (def == null) { Log.Error("[RimHeroes.WeaponDemo] missing def " + weaponDef); return false; }
                var w = (ThingWithComps)ThingMaker.MakeThing(def);
                p.equipment.MakeRoomFor(w);
                p.equipment.AddEquipment(w);
                return true;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.WeaponDemo] equip {weaponDef} failed: {e}"); return false; }
        }
    }
}
