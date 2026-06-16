using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Visual check for the Menagerie wildshape reskins + purple overlay + cast VFX: launch with
    /// -quicktest -rhformart (Menagerie must be active). Spawns a druid on cleared sand, then applies
    /// each form hediff directly and screenshots it: Displacer Beast, Basilisk, and the Faerie Dragon
    /// capstone. The game is left running so the cast gas/sparkle flecks animate. Logs whether each
    /// form's beast graphic resolved to a real texture (not the pink placeholder), then exits.
    /// </summary>
    public class GameComponent_FormArtSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhformart");

        private static readonly (string hediff, string shot)[] Forms =
        {
            ("RH_WildshapeDireWolf", "rhform_displacerbeast"),
            ("RH_WildshapeGiantElk", "rhform_basilisk"),
            ("RH_WildshapeDragon",   "rhform_faeriedragon"),
        };

        private int state;
        private int idx;
        private float nextTime = -1f;
        private Pawn hero;
        private bool ok = true;

        public GameComponent_FormArtSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 3) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextTime < 0f) { nextTime = now + 4f; return; }
            if (now < nextTime) return;

            switch (state)
            {
                case 0:
                {
                    var spot = map.Center + new IntVec3(28, 0, 0);
                    for (int dx = -6; dx <= 6; dx++)
                        for (int dz = -6; dz <= 6; dz++)
                        {
                            var c = spot + new IntVec3(dx, 0, dz);
                            if (!c.InBounds(map)) continue;
                            foreach (var t in c.GetThingList(map).ToList())
                                if (!(t is Pawn) && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                            map.terrainGrid.SetTerrain(c, TerrainDefOf.Sand);
                        }
                    hero = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                    GenSpawn.Spawn(hero, spot, map);
                    HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Druid"));
                    Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;   // keep meshes + flecks alive
                    Log.Message("[RimHeroes.FormArt] druid spawned; cycling Menagerie forms");
                    nextTime = now + 2f;
                    state = 1;
                    break;
                }
                case 1:
                {
                    // apply the next form; PostAdd swaps out any previous form + fires the cast VFX
                    var def = HediffDef.Named(Forms[idx].hediff);
                    var hediff = hero.health.AddHediff(def) as Hediff_Wildshape;
                    bool art = hediff?.BeastGraphic?.MatSouth != null
                               && hediff.BeastGraphic.MatSouth.mainTexture != null
                               && hediff.BeastGraphic.MatSouth.mainTexture.name != "BadTex";
                    if (!art) { ok = false; Log.Warning($"[RimHeroes.FormArt] FAIL: {Forms[idx].hediff} art did not resolve"); }
                    else Log.Message($"[RimHeroes.FormArt] OK: {Forms[idx].hediff} art = {hediff.BeastGraphic.MatSouth.mainTexture.name}");
                    foreach (var w in Find.WindowStack.Windows.ToList()) if (!(w is MainTabWindow)) w.Close(false);
                    Messages.Clear();
                    hero.jobs?.StopAll();
                    Find.Selector.ClearSelection();
                    Find.CameraDriver.SetRootPosAndSize(hero.Position.ToVector3Shifted(), 8f);
                    nextTime = now + 1.2f;   // let the cast VFX bloom before the shot
                    state = 2;
                    break;
                }
                case 2:
                {
                    Find.CameraDriver.SetRootPosAndSize(hero.Position.ToVector3Shifted(), 8f);
                    ScreenshotTaker.TakeNonSteamShot(Forms[idx].shot);
                    idx++;
                    if (idx >= Forms.Length) { nextTime = now + 1f; state = 3; }
                    else { nextTime = now + 0.4f; state = 1; }
                    break;
                }
                case 3:
                    Log.Message($"[RimHeroes.FormArt] RESULT: menagerie form art verdict={(ok ? "PASS" : "FAIL")}");
                    state = 4;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
