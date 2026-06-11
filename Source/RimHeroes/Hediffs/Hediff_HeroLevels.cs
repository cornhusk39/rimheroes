using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// The Hero progression engine, one per Hero pawn (VPE Hediff_PsycastAbilities pattern).
    /// Stores class, level, XP; applies per-level grants. Will grow: points, known spells,
    /// slot state, vestment state, gestral roster.
    /// </summary>
    public class Hediff_HeroLevels : HediffWithComps
    {
        public HeroClassDef classDef;
        public int level = 1;
        public float xp;

        public override string LabelInBrackets => classDef != null ? $"{classDef.label} {level}" : base.LabelInBrackets;

        public override bool ShouldRemove => false;

        // Placeholder curve (VPE-style: 100 XP at L1, x1.15 per level). Tune in playtest.
        public static float XPRequiredForLevel(int lvl) => 100f * Mathf.Pow(1.15f, lvl - 1);

        public float XPForNextLevel => XPRequiredForLevel(level);

        public bool AtMaxLevel => classDef == null || level >= classDef.maxLevel;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            ApplyGrants();
        }

        public override void TickInterval(int delta)
        {
            base.TickInterval(delta);
            // Passive "surviving the rim" trickle; VTR-safe via delta-aware hash interval.
            if (pawn.IsHashIntervalTick(RH_Tuning.PassiveXPIntervalTicks, delta))
            {
                GainXP(RH_Tuning.PassiveXPPerHour);
            }
            if (pawn.IsHashIntervalTick(60, delta))
            {
                CheckDeathsDoor();
            }
            if (pawn.IsHashIntervalTick(250, delta))
            {
                SyncGestrals();
            }
        }

        // ===== Gestral retinue =====

        private List<GestralBond> gestralBonds = new List<GestralBond>();

        public List<GestralBond> GestralBonds => gestralBonds;

        private const int InitialSpawnDelayTicks = 600;

        /// <summary>
        /// Keeps the retinue in sync with unlocks: creates bonds as levels unlock them, schedules
        /// walk-in replacements (1-3 days) when a gestral dies or departs, and spawns arrivals.
        /// Stops automatically while the master is dead (hediffs don't tick on corpses) and
        /// resumes on resurrection.
        /// </summary>
        private void SyncGestrals()
        {
            if (classDef?.gestralUnlocks == null || pawn.Dead)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            foreach (var unlock in classDef.gestralUnlocks)
            {
                if (unlock.level > level || unlock.job?.pawnKind == null)
                {
                    continue;
                }
                var bond = gestralBonds.FirstOrDefault(b => b.job == unlock.job);
                if (bond == null)
                {
                    bond = new GestralBond { job = unlock.job, respawnAtTick = now + InitialSpawnDelayTicks };
                    gestralBonds.Add(bond);
                    continue;
                }
                if (bond.gestral != null && (bond.gestral.Dead || bond.gestral.Destroyed || !bond.gestral.SpawnedOrAnyParentSpawned))
                {
                    bool died = bond.gestral.Dead || bond.gestral.Destroyed;
                    bond.gestral = null;
                    bond.respawnAtTick = now + Rand.Range(60000, 180000); // 1-3 days
                    if (died && PawnUtility.ShouldSendNotificationAbout(pawn))
                    {
                        Messages.Message("RH_GestralLost".Translate(unlock.job.LabelCap, pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent);
                    }
                }
                if (bond.gestral == null && now >= bond.respawnAtTick && pawn.Spawned && pawn.Map != null)
                {
                    SpawnGestral(bond);
                }
            }
        }

        private void SpawnGestral(GestralBond bond)
        {
            var map = pawn.Map;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal))
            {
                return;
            }
            var gestral = PawnGenerator.GeneratePawn(new PawnGenerationRequest(bond.job.pawnKind, pawn.Faction));
            GenSpawn.Spawn(gestral, cell, map);
            if (gestral.connections == null)
            {
                gestral.connections = new Pawn_ConnectionsTracker(gestral);
            }
            gestral.connections.ConnectTo(pawn);
            var devotion = (Hediff_GestralDevotion)gestral.health.AddHediff(RH_DefOf.RH_GestralDevotion);
            devotion.master = pawn;
            bond.gestral = gestral;
            bond.respawnAtTick = -1;
            Messages.Message("RH_GestralArrived".Translate(bond.job.LabelCap, pawn.LabelShortCap), gestral, MessageTypeDefOf.PositiveEvent);
        }

        public void DebugForceGestralRespawn()
        {
            foreach (var bond in gestralBonds)
            {
                if (bond.gestral == null)
                {
                    bond.respawnAtTick = 0;
                }
            }
        }

        /// <summary>
        /// Heroes carry preventsDeath (destiny), so health-driven death never fires. Instead, when
        /// they WOULD be dead, they fight for their life: death saving throws via Hediff_DeathsDoor.
        /// </summary>
        private void CheckDeathsDoor()
        {
            if (pawn.Dead || !pawn.Downed || !pawn.Spawned)
            {
                return;
            }
            var hediffSet = pawn.health.hediffSet;
            if (hediffSet.HasHediff(RH_DefOf.RH_DeathsDoor) || hediffSet.HasHediff(RH_DefOf.RH_Stabilized))
            {
                return;
            }
            if (!WouldBeDeadWithoutDestiny())
            {
                return;
            }
            pawn.health.AddHediff(RH_DefOf.RH_DeathsDoor);
            Find.LetterStack.ReceiveLetter("RH_DeathsDoorLabel".Translate(pawn.LabelShortCap),
                "RH_DeathsDoorText".Translate(pawn.LabelShortCap), LetterDefOf.NegativeEvent, pawn);
        }

        /// <summary>Replicates Pawn_HealthTracker.ShouldBeDead minus the preventsDeath early-out.</summary>
        public bool WouldBeDeadWithoutDestiny()
        {
            var health = pawn.health;
            foreach (var hediff in health.hediffSet.hediffs)
            {
                if (hediff.CauseDeathNow())
                {
                    return true;
                }
            }
            if (health.ShouldBeDeadFromRequiredCapacity() != null)
            {
                return true;
            }
            if (PawnCapacityUtility.CalculatePartEfficiency(health.hediffSet, pawn.RaceProps.body.corePart) <= 0.0001f)
            {
                return true;
            }
            return health.ShouldBeDeadFromLethalDamageThreshold();
        }

        public void Notify_FailedDeathSaves()
        {
            // Hook for future bookkeeping (gestral panic event, revival quest seeding).
        }

        public void GainXP(float amount)
        {
            if (classDef == null || amount <= 0f || AtMaxLevel)
            {
                return;
            }
            xp += amount;
            bool leveled = false;
            while (!AtMaxLevel && xp >= XPRequiredForLevel(level))
            {
                xp -= XPRequiredForLevel(level);
                level++;
                leveled = true;
            }
            if (leveled)
            {
                ApplyGrants();
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message("RH_LeveledUp".Translate(pawn.LabelShortCap, classDef.label, level),
                        pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
            if (AtMaxLevel)
            {
                xp = 0f;
            }
        }

        /// <summary>Idempotent: applies every grant at or below the current level.</summary>
        private void ApplyGrants()
        {
            if (classDef?.levelGrants == null)
            {
                return;
            }
            foreach (var grant in classDef.levelGrants)
            {
                if (grant.level > level)
                {
                    continue;
                }
                if (grant.abilities != null)
                {
                    foreach (var abilityDef in grant.abilities)
                    {
                        pawn.abilities?.GainAbility(abilityDef); // dup-safe in vanilla
                    }
                }
                if (grant.features != null)
                {
                    foreach (var featureDef in grant.features)
                    {
                        if (!pawn.health.hediffSet.HasHediff(featureDef))
                        {
                            pawn.health.AddHediff(featureDef);
                        }
                    }
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref classDef, "classDef");
            Scribe_Values.Look(ref level, "level", 1);
            Scribe_Values.Look(ref xp, "xp");
            Scribe_Collections.Look(ref gestralBonds, "gestralBonds", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && gestralBonds == null)
            {
                gestralBonds = new List<GestralBond>();
            }
        }
    }

    public class GestralBond : IExposable
    {
        public GestralJobDef job;
        public Pawn gestral;
        public int respawnAtTick = -1;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref job, "job");
            Scribe_References.Look(ref gestral, "gestral");
            Scribe_Values.Look(ref respawnAtTick, "respawnAtTick", -1);
        }
    }
}
