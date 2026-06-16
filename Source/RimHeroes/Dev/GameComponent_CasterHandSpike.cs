using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Caster weapon-placement review: -quicktest -rhcasterhands. Spawns the four staff casters
    /// (Wizard, Sorcerer, Cleric, Druid) at L20 with their T5 weapons, drafted and south-facing on
    /// cleared sand, frames them tight, and screenshots "rhcasterhands" for before/after comparison.
    /// </summary>
    public class GameComponent_CasterHandSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhcasterhands");

        private static readonly string[] Casters = { "Wizard", "Sorcerer", "Cleric", "Druid" };

        private int state;
        private float nextStateTime = -1f;
        private readonly List<Pawn> pawns = new List<Pawn>();
        private IntVec3 anchor;

        public GameComponent_CasterHandSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 3) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f) { nextStateTime = now + 4f; return; }
            if (now < nextStateTime) return;

            switch (state)
            {
                case 0:
                    Patch_CasterWeaponOffset.ShiftEnabled = !GenCommandLine.CommandLineArgPassed("rhnoshift");
                    anchor = map.Center + new IntVec3(0, 0, -8);   // fixed so before/after frame identically
                    ClearSand(map, 14, 10);
                    for (int i = 0; i < Casters.Length; i++)
                    {
                        var cell = anchor + new IntVec3(i * 3 - 4, 0, 0);
                        var p = SpawnCaster(map, cell, "RH_" + Casters[i]);
                        if (p != null) pawns.Add(p);
                    }
                    foreach (var p in pawns) { p.jobs?.StopAll(); p.Rotation = Rot4.South; }
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 3.6f);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;   // keep ticking so held meshes build
                    nextStateTime = now + 3f;
                    state = 1;
                    break;
                case 1:
                    // do NOT pause: drafted pawns hold position, and the running clock lets the held
                    // weapon draw meshes rebuild (a paused spawn renders weaponless)
                    foreach (var w in Find.WindowStack.Windows.ToList())
                        if (!(w is MainTabWindow)) w.Close(false);
                    Find.LetterStack?.LettersListForReading?.Clear();
                    Messages.Clear();
                    foreach (var p in pawns) { p.jobs?.StopAll(); p.Rotation = Rot4.South; }
                    Find.CameraDriver.SetRootPosAndSize(anchor.ToVector3(), 3.6f);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    nextStateTime = now + 1.5f;
                    state = 2;
                    break;
                case 2:
                    foreach (var w in Find.WindowStack.Windows.ToList())
                        if (!(w is MainTabWindow)) w.Close(false);
                    foreach (var p in pawns) { p.jobs?.StopAll(); p.Rotation = Rot4.South; }
                    ScreenshotTaker.TakeNonSteamShot("rhcasterhands");
                    nextStateTime = now + 1.5f;
                    state = 3;
                    break;
                case 3:
                    Log.Message("[RimHeroes.CasterHands] RESULT: caster weapon shot taken verdict=PASS");
                    state = 4;
                    Root.Shutdown();
                    break;
            }
        }

        private Pawn SpawnCaster(Map map, IntVec3 cell, string cls)
        {
            try
            {
                var classDef = DefDatabase<HeroClassDef>.GetNamedSilentFail(cls);
                if (classDef == null) return null;
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer,
                    fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true, fixedGender: Gender.Male));
                GenSpawn.Spawn(p, cell, map);
                if (p.story != null) p.story.bodyType = BodyTypeDefOf.Male;
                p.apparel?.DestroyAll();
                p.equipment?.DestroyAllEquipment();
                var levels = HeroUtility.MakeHero(p, classDef);
                levels.SetLevelDirect(20);
                var def = DefDatabase<ThingDef>.GetNamedSilentFail($"RH_Weapon_{cls.Replace("RH_", "")}_{HeroUtility.WeaponTierSuffix(5)}");
                if (def != null)
                {
                    var wpn = (ThingWithComps)ThingMaker.MakeThing(def);
                    p.equipment.MakeRoomFor(wpn);
                    p.equipment.AddEquipment(wpn);
                }
                p.Name = new NameSingle(cls.Replace("RH_", ""));
                p.Drawer?.renderer?.SetAllGraphicsDirty();
                if (p.drafter != null) p.drafter.Drafted = true;
                return p;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.CasterHands] {cls} failed: {e}"); return null; }
        }

        private void ClearSand(Map map, int halfW, int halfH)
        {
            for (int dx = -halfW; dx <= halfW; dx++)
                for (int dz = -halfH; dz <= halfH; dz++)
                {
                    var c = anchor + new IntVec3(dx, 0, dz);
                    if (!c.InBounds(map)) continue;
                    foreach (var t in c.GetThingList(map).ToList())
                        if (!(t is Pawn) && t.def != null && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Sand);
                    map.snowGrid?.SetDepth(c, 0f);
                    map.roofGrid?.SetRoof(c, null);
                }
        }
    }
}
