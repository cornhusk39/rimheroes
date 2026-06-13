using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Weapon showcase + equip-gate test: -quicktest -rhweapondemo. Spawns an L20 Barbarian holding
    /// the Worldcleaver, screenshots, and logs CanEquip results for wrong-class / too-low-level /
    /// correct cases, then exits.
    /// </summary>
    public class GameComponent_WeaponDemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhweapondemo");

        private int state;
        private float nextStateTime = -1f;
        private Pawn barb;
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
                    var searchFrom = map.Center + new IntVec3(0, 0, -35);
                    CellFinder.TryFindRandomCellNear(searchFrom, map, 10, c => c.Standable(map), out anchor);
                    barb = SpawnHero(map, anchor, "RH_Barbarian", 20);
                    EquipOn(barb, "RH_Weapon_Barbarian_T5");
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 9f);
                    RunGateTests(map);
                    state = 1;
                    break;
                }
                case 1:
                    Find.TickManager.Pause();
                    barb.jobs?.StopAll();
                    barb.Rotation = Rot4.South;
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 9f);
                    state = 2;
                    break;
                case 2:
                    ScreenshotTaker.TakeNonSteamShot("rhweapon_south");
                    barb.Rotation = Rot4.East;
                    state = 3;
                    break;
                case 3:
                    ScreenshotTaker.TakeNonSteamShot("rhweapon_east");
                    state = 4;
                    break;
                case 4:
                    Log.Message("[RimHeroes.WeaponDemo] RESULT: weapon demo done verdict=PASS");
                    state = 5;
                    Root.Shutdown();
                    break;
            }
        }

        private Pawn SpawnHero(Map map, IntVec3 cell, string cls, int level)
        {
            var classDef = DefDatabase<HeroClassDef>.GetNamed(cls);
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

        private void EquipOn(Pawn p, string weaponDef)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDef);
            if (def == null) { Log.Error("[RimHeroes.WeaponDemo] missing def " + weaponDef); return; }
            var w = (ThingWithComps)ThingMaker.MakeThing(def);
            p.equipment.MakeRoomFor(w);
            p.equipment.AddEquipment(w);
        }

        private void RunGateTests(Map map)
        {
            void Test(string label, Pawn pawn, string weaponDef, bool expect)
            {
                var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDef);
                var w = (ThingWithComps)ThingMaker.MakeThing(def);
                bool can = EquipmentUtility.CanEquip(w, pawn, out string reason, false);
                string mark = can == expect ? "OK" : "FAIL";
                Log.Message($"[RimHeroes.WeaponDemo] GATE {mark} ({label}): can={can} expected={expect} reason='{reason}'");
            }

            var wiz = SpawnHero(map, anchor + new IntVec3(4, 0, 0), "RH_Wizard", 20);
            var lowBarb = SpawnHero(map, anchor + new IntVec3(8, 0, 0), "RH_Barbarian", 1);
            Test("wizard L20 tries barb Lesser", wiz, "RH_Weapon_Barbarian_T1", false);
            Test("barb L1 tries Hero axe (needs L5)", lowBarb, "RH_Weapon_Barbarian_T2", false);
            Test("barb L1 tries Lesser axe", lowBarb, "RH_Weapon_Barbarian_T1", true);
            Test("barb L20 tries Worldcleaver", barb, "RH_Weapon_Barbarian_T5", true);
        }
    }
}
