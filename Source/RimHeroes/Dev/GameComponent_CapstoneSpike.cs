using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Capstone payoff check: -quicktest -rhcapstone. Confirms the class -> capstone-weapon mapping, that
    /// MarkCapstoneDungeon drops a tier-3 capstone site carrying that weapon, and that delving such a
    /// dungeon yields the Legendary weapon in its vault. Logs assertions + a verdict, then exits.
    /// </summary>
    public class GameComponent_CapstoneSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhcapstone");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_CapstoneSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var src = Find.CurrentMap;
            if (src == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.Capstone] === capstone payoff ===");
            var fighter = DefDatabase<HeroClassDef>.GetNamedSilentFail("RH_Fighter");
            var weapon = CapstoneQuest.CapstoneWeaponFor(fighter);
            Assert(weapon != null && weapon.defName == "RH_Weapon_Fighter_T5", $"class maps to capstone weapon ({weapon?.defName})");

            var site = CapstoneQuest.MarkCapstoneDungeon(fighter);
            var comp = (site as WorldObject)?.GetComponent<WorldObjectComp_DungeonSite>();
            bool capstoneKind = comp != null && (comp.kind?.defName == "RH_Dungeon_HundredEyes" || comp.kind?.defName == "RH_Dungeon_AnnihilatorForge");
            Assert(site != null && comp?.capstoneWeapon == weapon && comp.tier == 3 && capstoneKind,
                $"marks a tier-3 capstone site carrying the weapon (kind {comp?.kind?.defName})");

            // delving such a dungeon yields the Legendary weapon in the vault
            var gen = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("RH_DungeonGen");
            Map dungeon = null;
            try
            {
                Building_DungeonEntrance.DebugForcedKind = DefDatabase<DungeonKindDef>.GetNamed("RH_Dungeon_HundredEyes");
                Building_DungeonEntrance.DebugForcedTier = 3;
                Building_DungeonEntrance.DebugForcedDifficulty = 2.4f;
                Building_DungeonEntrance.DebugForcedCapstoneWeapon = weapon;
                dungeon = PocketMapUtility.GeneratePocketMap(new IntVec3(64, 1, 64), gen, null, src);
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.Capstone] gen failed: {e}"); ok = false; }
            finally
            {
                Building_DungeonEntrance.DebugForcedKind = null;
                Building_DungeonEntrance.DebugForcedTier = 1;
                Building_DungeonEntrance.DebugForcedDifficulty = 1f;
                Building_DungeonEntrance.DebugForcedCapstoneWeapon = null;
            }
            var dropped = dungeon?.listerThings.AllThings.FirstOrDefault(t => t.def == weapon);
            bool legendary = dropped?.TryGetComp<CompQuality>()?.Quality == QualityCategory.Legendary;
            Assert(dropped != null && legendary, $"capstone dungeon vault yields the Legendary weapon (found={dropped != null}, legendary={legendary})");

            Log.Message($"[RimHeroes.Capstone] RESULT: capstone payoff verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.Capstone] OK: {what}");
            else { Log.Warning($"[RimHeroes.Capstone] FAIL: {what}"); ok = false; }
        }
    }
}
