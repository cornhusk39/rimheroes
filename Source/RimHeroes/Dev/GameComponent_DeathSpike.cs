using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated death-saves test: launch with -quicktest -rhdeathspike.
    /// Phase A: make a hero, push blood loss to lethal -> expect alive + downed + death's door.
    /// Phase B: force three successful rolls -> expect stabilized, still alive.
    /// Phase C: re-trigger, force three failed rolls -> expect actually dead.
    /// Deterministic via Hediff_DeathsDoor.ResolveRoll(roll).
    /// </summary>
    public class GameComponent_DeathSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhdeathspike");

        private int state;
        private int attempts;
        private float nextStateTime = -1f;
        private Pawn pawn;

        public GameComponent_DeathSpike(Game game) { }

        private Hediff_DeathsDoor Door => pawn.health.hediffSet.GetFirstHediffOfDef(RH_DefOf.RH_DeathsDoor) as Hediff_DeathsDoor;

        public override void GameComponentUpdate()
        {
            if (!Active || state > 6)
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

            switch (state)
            {
                case 0:
                    pawn = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                    if (pawn == null)
                    {
                        pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(pawn, map.Center, map);
                    }
                    HeroUtility.MakeHero(pawn, DefDatabase<HeroClassDef>.GetNamed("RH_Fighter"));
                    SetBloodLoss(1.0f);
                    Log.Message($"[RimHeroes.DeathSpike] hero={pawn.LabelShort}, blood loss set lethal; waiting for death's door");
                    state = 1;
                    break;
                case 1: // wait for death's door to trigger (60-tick check interval)
                    if (Door != null)
                    {
                        Log.Message($"[RimHeroes.DeathSpike] PhaseA: alive={!pawn.Dead} downed={pawn.Downed} door=present");
                        state = 2;
                        attempts = 0;
                    }
                    else if (++attempts > 8)
                    {
                        Log.Message($"[RimHeroes.DeathSpike] RESULT: verdict=FAIL (door never appeared; alive={!pawn.Dead} downed={pawn.Downed})");
                        state = 6;
                    }
                    break;
                case 2: // three forced successes -> stabilized
                {
                    var door = Door;
                    door.ResolveRoll(15);
                    door.ResolveRoll(15);
                    door.ResolveRoll(15);
                    bool stabilized = pawn.health.hediffSet.HasHediff(RH_DefOf.RH_Stabilized);
                    bool doorGone = Door == null;
                    Log.Message($"[RimHeroes.DeathSpike] PhaseB: alive={!pawn.Dead} stabilized={stabilized} doorGone={doorGone}");
                    // reset for phase C: clear stabilized so the door can re-trigger
                    var stab = pawn.health.hediffSet.GetFirstHediffOfDef(RH_DefOf.RH_Stabilized);
                    if (stab != null)
                    {
                        pawn.health.RemoveHediff(stab);
                    }
                    SetBloodLoss(1.0f);
                    state = 3;
                    attempts = 0;
                    break;
                }
                case 3: // wait for re-trigger
                    if (Door != null)
                    {
                        Log.Message("[RimHeroes.DeathSpike] PhaseC: door re-triggered");
                        state = 4;
                    }
                    else if (++attempts > 8)
                    {
                        Log.Message("[RimHeroes.DeathSpike] RESULT: verdict=FAIL (door did not re-trigger)");
                        state = 6;
                    }
                    break;
                case 4: // three forced failures -> actually dead
                {
                    var door = Door;
                    door.ResolveRoll(5);
                    door.ResolveRoll(5);
                    door.ResolveRoll(5);
                    Log.Message($"[RimHeroes.DeathSpike] PhaseC rolls done: dead={pawn.Dead}");
                    state = 5;
                    break;
                }
                case 5:
                {
                    string verdict = pawn.Dead ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.DeathSpike] RESULT: dead={pawn.Dead} verdict={verdict}");
                    state = 6;
                    break;
                }
                case 6:
                    state = 7;
                    Root.Shutdown();
                    break;
            }
        }

        private void SetBloodLoss(float severity)
        {
            var bloodLoss = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss);
            if (bloodLoss == null)
            {
                bloodLoss = pawn.health.AddHediff(HediffDefOf.BloodLoss);
            }
            bloodLoss.Severity = severity;
        }
    }
}
