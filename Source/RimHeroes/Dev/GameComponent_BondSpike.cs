using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated gestral-bonding test: launch with -quicktest -rhbondspike.
    /// Phase A: Fighter L5 hero -> Porter gestral walks in, bonded + devoted.
    /// Phase B: gestral killed -> replacement scheduled -> forced -> new one walks in.
    /// Phase C: master killed -> devotion goes bereft, gestral panics; leave phase forced -> exits map.
    /// Phase D: master resurrected -> respawn forced -> fresh gestral arrives.
    /// </summary>
    public class GameComponent_BondSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhbondspike");

        private int state;
        private int attempts;
        private float nextStateTime = -1f;
        private Pawn hero;
        private bool passA, passB, passC, passD;

        public GameComponent_BondSpike(Game game) { }

        private Hediff_HeroLevels Hero => HeroUtility.GetHeroHediff(hero);

        private Pawn Gestral => Hero?.GestralBonds.FirstOrDefault()?.gestral;

        public override void GameComponentUpdate()
        {
            if (!Active || state > 9)
            {
                return;
            }
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f)
            {
                nextStateTime = now + 4f;
                return;
            }
            if (now < nextStateTime)
            {
                return;
            }
            nextStateTime = now + 3f;
            if (Find.TickManager.Paused)
            {
                Find.TickManager.CurTimeSpeed = TimeSpeed.Normal; // death letters may auto-pause; syncs are tick-driven
            }

            switch (state)
            {
                case 0:
                    hero = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                    if (hero == null)
                    {
                        hero = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(hero, map.Center, map);
                    }
                    HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Fighter"));
                    Hero.GainXP(600f); // L5 -> Porter (L3) unlocked
                    Log.Message($"[RimHeroes.BondSpike] hero={hero.LabelShort} level={Hero.level}; waiting for porter walk-in");
                    state = 1;
                    attempts = 0;
                    break;
                case 1: // wait for first arrival
                {
                    var g = Gestral;
                    if (g != null && g.Spawned)
                    {
                        bool bonded = g.connections?.ConnectedThings.Contains(hero) ?? false;
                        var dev = g.GetDevotion();
                        passA = bonded && dev?.master == hero && g.Faction == Faction.OfPlayer && g.kindDef.defName == "RH_GestralPorterKind";
                        Log.Message($"[RimHeroes.BondSpike] PhaseA: arrived={g.LabelShort} kind={g.kindDef.defName} bonded={bonded} devotionMaster={(dev?.master == hero)} state={dev?.StateNow} pass={passA}");
                        g.Kill(null);
                        state = 2;
                        attempts = 0;
                    }
                    else if (++attempts > 15)
                    {
                        Log.Message("[RimHeroes.BondSpike] RESULT: verdict=FAIL (porter never arrived)");
                        state = 9;
                    }
                    break;
                }
                case 2: // wait for loss detection, then force respawn
                {
                    var bond = Hero.GestralBonds.FirstOrDefault();
                    if (bond != null && bond.gestral == null && bond.respawnAtTick > 0)
                    {
                        Log.Message($"[RimHeroes.BondSpike] PhaseB: loss detected, replacement in {(bond.respawnAtTick - Find.TickManager.TicksGame) / 2500f:F1}h; forcing now");
                        Hero.DebugForceGestralRespawn();
                        state = 3;
                        attempts = 0;
                    }
                    else if (++attempts > 10)
                    {
                        Log.Message("[RimHeroes.BondSpike] RESULT: verdict=FAIL (death not detected by roster)");
                        state = 9;
                    }
                    break;
                }
                case 3: // wait for replacement
                {
                    var g = Gestral;
                    if (g != null && g.Spawned && !g.Dead)
                    {
                        passB = true;
                        Log.Message($"[RimHeroes.BondSpike] PhaseB: replacement {g.LabelShort} arrived. Killing master...");
                        hero.Kill(null);
                        state = 4;
                        attempts = 0;
                    }
                    else if (++attempts > 15)
                    {
                        Log.Message("[RimHeroes.BondSpike] RESULT: verdict=FAIL (replacement never arrived)");
                        state = 9;
                    }
                    break;
                }
                case 4: // wait for bereft panic
                {
                    var g = Gestral;
                    var dev = g?.GetDevotion();
                    if (dev?.StateNow == DevotionState.Bereft && dev.masterDeadTick >= 0)
                    {
                        Log.Message($"[RimHeroes.BondSpike] PhaseC: bereft, panicking (job={g.CurJobDef?.defName}); forcing leave phase");
                        dev.DebugForceLeavePhase();
                        state = 5;
                        attempts = 0;
                    }
                    else if (++attempts > 10)
                    {
                        Log.Message($"[RimHeroes.BondSpike] RESULT: verdict=FAIL (no bereft state; state={dev?.StateNow})");
                        state = 9;
                    }
                    break;
                }
                case 5: // wait for map exit
                {
                    var g = Hero?.GestralBonds.FirstOrDefault()?.gestral;
                    if (g == null || !g.Spawned)
                    {
                        passC = true;
                        Log.Message("[RimHeroes.BondSpike] PhaseC: gestral left the map. Resurrecting master...");
                        ResurrectionUtility.TryResurrect(hero);
                        state = 6;
                        attempts = 0;
                    }
                    else if (++attempts > 25)
                    {
                        Log.Message($"[RimHeroes.BondSpike] RESULT: verdict=FAIL (gestral never left; job={g.CurJobDef?.defName})");
                        state = 9;
                    }
                    else
                    {
                        var dev = g.GetDevotion();
                        bool exitSpot = RCellFinder.TryFindRandomExitSpot(g, out _, TraverseMode.ByPawn);
                        Log.Message($"[RimHeroes.BondSpike] C-diag: state={dev?.StateNow} leave={dev?.InLeavePhase} deadTick={dev?.masterDeadTick} now={Find.TickManager.TicksGame} canReachEdge={g.CanReachMapEdge()} exitSpot={exitSpot} job={g.CurJobDef?.defName}");
                    }
                    break;
                }
                case 6: // wait for roster to notice departure post-resurrection, then force respawn
                {
                    if (hero.Dead)
                    {
                        Log.Message("[RimHeroes.BondSpike] RESULT: verdict=FAIL (resurrection failed)");
                        state = 9;
                        break;
                    }
                    var bond = Hero.GestralBonds.FirstOrDefault();
                    if (bond != null && bond.gestral == null)
                    {
                        Hero.DebugForceGestralRespawn();
                        state = 7;
                        attempts = 0;
                    }
                    else if (++attempts > 10)
                    {
                        Log.Message("[RimHeroes.BondSpike] RESULT: verdict=FAIL (departure not detected after resurrection)");
                        state = 9;
                    }
                    break;
                }
                case 7: // wait for fresh gestral
                {
                    var g = Gestral;
                    if (g != null && g.Spawned && !g.Dead)
                    {
                        passD = g.GetDevotion()?.StateNow == DevotionState.Content;
                        Log.Message($"[RimHeroes.BondSpike] PhaseD: fresh gestral {g.LabelShort} arrived, devotion={g.GetDevotion()?.StateNow}");
                        state = 8;
                    }
                    else if (++attempts > 15)
                    {
                        Log.Message("[RimHeroes.BondSpike] RESULT: verdict=FAIL (no gestral after resurrection)");
                        state = 9;
                    }
                    break;
                }
                case 8:
                {
                    string verdict = passA && passB && passC && passD ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.BondSpike] RESULT: A={passA} B={passB} C={passC} D={passD} verdict={verdict}");
                    state = 9;
                    break;
                }
                case 9:
                    state = 10;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
