using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Colony-entrance trickle check: -quicktest -rhtrickle. Confirms an entrance leaks hostiles when
    /// it spawns, that damage shortens the spawn interval, that sealing stops the trickle, and that a
    /// dying boss seals its source entrance. Logs assertions + a verdict, then exits.
    /// </summary>
    public class GameComponent_TrickleSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhtrickle");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_TrickleSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.Trickle] === colony-entrance trickle ===");
            var crypt = DefDatabase<DungeonKindDef>.GetNamedSilentFail("RH_Dungeon_Crypt");

            var entrance = MakeEntrance(map, new IntVec3(map.Center.x + 8, 0, map.Center.z), crypt, 2f);
            int before = Hostiles(map);
            entrance.SpawnTrickle();
            entrance.SpawnTrickle();
            entrance.SpawnTrickle();
            Assert(Hostiles(map) > before, $"trickle leaks hostiles ({before} -> {Hostiles(map)})");

            int i1 = entrance.SpawnIntervalTicks();
            entrance.PostApplyDamage(new DamageInfo(DamageDefOf.Blunt, 20f), 20f);
            int i2 = entrance.SpawnIntervalTicks();
            Assert(i2 < i1, $"damage shortens the spawn interval ({i1} -> {i2})");

            entrance.Seal();
            int afterSeal = Hostiles(map);
            entrance.SpawnTrickle();
            Assert(entrance.IsSealed && Hostiles(map) == afterSeal, "sealing stops the trickle");

            // a dying boss seals its source entrance
            var entrance2 = MakeEntrance(map, new IntVec3(map.Center.x - 8, 0, map.Center.z), crypt, 1f);
            var boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfEntities, forceGenerateNewPawn: true));
            GenSpawn.Spawn(boss, map.Center, map);
            if (boss.health.AddHediff(HediffDef.Named("RH_DungeonBoss")) is Hediff_DungeonBoss bh) bh.sourceEntrance = entrance2;
            boss.Kill(null);
            Assert(entrance2.IsSealed, "killing the boss seals its source entrance");

            Log.Message($"[RimHeroes.Trickle] RESULT: trickle verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private static Building_DungeonEntrance MakeEntrance(Map map, IntVec3 pos, DungeonKindDef kind, float difficulty)
        {
            foreach (var c in CellRect.CenteredOn(pos, 3, 3).Cells.Where(c => c.InBounds(map)))
                foreach (var t in c.GetThingList(map).ToList())
                    if (t.def.destroyable && !(t is Pawn)) t.Destroy(DestroyMode.Vanish);
            var def = DefDatabase<ThingDef>.GetNamed("RH_DungeonEntrance");
            var e = (Building_DungeonEntrance)ThingMaker.MakeThing(def);
            e.kind = kind;
            e.difficulty = difficulty;
            GenSpawn.Spawn(e, pos, map);
            return e;
        }

        private static int Hostiles(Map map) => map.mapPawns.AllPawnsSpawned.Count(p => !p.Dead && p.HostileTo(Faction.OfPlayer));

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.Trickle] OK: {what}");
            else { Log.Warning($"[RimHeroes.Trickle] FAIL: {what}"); ok = false; }
        }
    }
}
