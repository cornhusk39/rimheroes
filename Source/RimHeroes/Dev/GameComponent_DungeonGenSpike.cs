using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Dungeon generation review: -quicktest -rhdungeongen. Generates several DungeonKinds in turn
    /// (forcing each via Building_DungeonEntrance.DebugForcedKind), confirms each carved + populated
    /// without errors, and screenshots the layout. Output: Screenshots/rhdungeon_&lt;kind&gt;.png.
    /// </summary>
    public class GameComponent_DungeonGenSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhdungeongen");

        private static readonly string[] KindsToShow =
        {
            "RH_Dungeon_Crypt", "RH_Dungeon_HundredEyes", "RH_Dungeon_ConstructVault",
            "RH_Dungeon_BeastWarren", "RH_Dungeon_MedusaGallery",
        };

        private int idx;
        private int state;
        private float nextTime = -1f;
        private Map dungeon;
        private Map src;
        private Pawn bossShotTarget;
        private bool ok = true;

        public GameComponent_DungeonGenSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 5) return;
            if (src == null) src = Find.CurrentMap;
            if (src == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextTime < 0f) { nextTime = now + 4f; return; }
            if (now < nextTime) return;

            switch (state)
            {
                case 0:
                {
                    var gen = DefDatabase<MapGeneratorDef>.GetNamedSilentFail("RH_DungeonGen");
                    var kind = DefDatabase<DungeonKindDef>.GetNamedSilentFail(KindsToShow[idx]);
                    if (gen == null || kind == null) { Log.Error($"[RimHeroes.DungeonGen] missing gen/kind {KindsToShow[idx]}"); ok = false; state = 3; return; }
                    try
                    {
                        Building_DungeonEntrance.DebugForcedKind = kind;
                        Building_DungeonEntrance.DebugForcedTier = 3;        // exercise max-tier scaling
                        Building_DungeonEntrance.DebugForcedDifficulty = 2.4f;
                        dungeon = PocketMapUtility.GeneratePocketMap(new IntVec3(64, 1, 64), gen, null, src);
                    }
                    catch (System.Exception e) { Log.Error($"[RimHeroes.DungeonGen] {kind.defName} gen failed: {e}"); ok = false; ResetDebug(); state = 3; return; }
                    finally { ResetDebug(); }

                    Current.Game.CurrentMap = dungeon;
                    var comp = dungeon.GetComponent<MapComponent_Dungeon>();
                    int monsters = dungeon.mapPawns.AllPawnsSpawned.Count(p => p.HostileTo(Faction.OfPlayer));
                    bool boss = dungeon.mapPawns.AllPawnsSpawned.Any(p => p.health?.hediffSet?.GetFirstHediffOfDef(HediffDef.Named("RH_DungeonBoss")) != null);
                    bool reliquary = dungeon.listerThings.AllThings.Any(t => t.def.defName == "RH_Reliquary");
                    bool kindOk = comp?.kind == kind && (comp?.rooms?.Count ?? 0) >= 3 && monsters > 0 && boss && reliquary;
                    if (!kindOk) ok = false;
                    Log.Message($"[RimHeroes.DungeonGen] {kind.defName} (T{comp?.tier} x{comp?.difficulty:F1}): rooms={comp?.rooms?.Count ?? -1} hostiles={monsters} boss={boss} reliquary={reliquary} {(kindOk ? "OK" : "FAIL")}");
                    if (comp != null && comp.entranceIndex >= 0 && comp.entranceIndex < comp.rooms.Count)
                        FloodFillerFog.FloodUnfog(comp.rooms[comp.entranceIndex].CenterCell, dungeon);
                    Find.CameraDriver.SetRootPosAndSize(dungeon.Center.ToVector3(), 36f);
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
                    nextTime = now + 2.5f;
                    state = 1;
                    break;
                }
                case 1:
                {
                    foreach (var w in Find.WindowStack.Windows.ToList()) if (!(w is MainTabWindow)) w.Close(false);
                    Messages.Clear();
                    Find.CameraDriver.SetRootPosAndSize(dungeon.Center.ToVector3(), 36f);
                    ScreenshotTaker.TakeNonSteamShot("rhdungeon_" + KindsToShow[idx].Replace("RH_Dungeon_", ""));
                    // close-up on the boss so its scale + aura can be eyeballed
                    var bossPawn = dungeon.mapPawns.AllPawnsSpawned.FirstOrDefault(p =>
                        p.health?.hediffSet?.GetFirstHediffOfDef(HediffDef.Named("RH_DungeonBoss")) != null);
                    if (bossPawn != null) Find.CameraDriver.SetRootPosAndSize(bossPawn.Position.ToVector3Shifted(), 9f);
                    nextTime = now + 1f;
                    state = 5;
                    bossShotTarget = bossPawn;
                    break;
                }
                case 5:
                    if (bossShotTarget != null)
                    {
                        Find.CameraDriver.SetRootPosAndSize(bossShotTarget.Position.ToVector3Shifted(), 9f);
                        ScreenshotTaker.TakeNonSteamShot("rhboss_" + KindsToShow[idx].Replace("RH_Dungeon_", ""));
                    }
                    nextTime = now + 0.5f;
                    state = 2;
                    break;
                case 2:
                    if (dungeon != null) PocketMapUtility.DestroyPocketMap(dungeon);
                    dungeon = null;
                    idx++;
                    if (idx >= KindsToShow.Length) { nextTime = now + 0.5f; state = 3; }
                    else { Current.Game.CurrentMap = src; nextTime = now + 0.5f; state = 0; }
                    break;
                case 3:
                    Log.Message($"[RimHeroes.DungeonGen] RESULT: dungeon kinds verdict={(ok ? "PASS" : "FAIL")}");
                    state = 6;
                    Root.Shutdown();
                    break;
            }
        }

        private static void ResetDebug()
        {
            Building_DungeonEntrance.DebugForcedKind = null;
            Building_DungeonEntrance.DebugForcedTier = 1;
            Building_DungeonEntrance.DebugForcedDifficulty = 1f;
        }
    }
}
