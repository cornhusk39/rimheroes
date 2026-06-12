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
    /// slot state, vestment state, mim roster.
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

        public Hediff_ClassVestment Vestment =>
            classDef?.vestmentHediff == null
                ? null
                : pawn.health.hediffSet.GetFirstHediffOfDef(classDef.vestmentHediff) as Hediff_ClassVestment;

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
                SyncMims();
            }
            TickRests(delta);
            if (pawn.IsHashIntervalTick(30, delta))
            {
                TickAutocast();
            }
        }

        // ===== Mim retinue =====

        private List<MimBond> mimBonds = new List<MimBond>();

        public List<MimBond> MimBonds => mimBonds;

        private const int InitialSpawnDelayTicks = 600;

        /// <summary>
        /// Keeps the retinue in sync with unlocks: creates bonds as levels unlock them, schedules
        /// walk-in replacements (1-3 days) when a mim dies or departs, and spawns arrivals.
        /// Stops automatically while the master is dead (hediffs don't tick on corpses) and
        /// resumes on resurrection.
        /// </summary>
        private void SyncMims()
        {
            // Player heroes only: enemy heroes don't trail walk-in retinues (their combat
            // mims will come with raid composition work later).
            if (classDef?.mimUnlocks == null || pawn.Dead || pawn.Faction != Faction.OfPlayer)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            foreach (var unlock in classDef.mimUnlocks)
            {
                if (unlock.level > level || unlock.job?.pawnKind == null)
                {
                    continue;
                }
                var bond = mimBonds.FirstOrDefault(b => b.job == unlock.job);
                if (bond == null)
                {
                    bond = new MimBond { job = unlock.job, respawnAtTick = now + InitialSpawnDelayTicks };
                    mimBonds.Add(bond);
                    continue;
                }
                if (bond.mim != null && (bond.mim.Dead || bond.mim.Destroyed || !bond.mim.SpawnedOrAnyParentSpawned))
                {
                    bool died = bond.mim.Dead || bond.mim.Destroyed;
                    bond.mim = null;
                    bond.respawnAtTick = now + Rand.Range(60000, 180000); // 1-3 days
                    if (died && PawnUtility.ShouldSendNotificationAbout(pawn))
                    {
                        Messages.Message("RH_MimLost".Translate(unlock.job.LabelCap, pawn.LabelShortCap), pawn, MessageTypeDefOf.NegativeEvent);
                    }
                }
                if (bond.mim == null && now >= bond.respawnAtTick && pawn.Spawned && pawn.Map != null)
                {
                    SpawnMim(bond);
                }
            }
        }

        private void SpawnMim(MimBond bond)
        {
            var map = pawn.Map;
            if (!RCellFinder.TryFindRandomPawnEntryCell(out var cell, map, CellFinder.EdgeRoadChance_Animal))
            {
                return;
            }
            var mim = PawnGenerator.GeneratePawn(new PawnGenerationRequest(bond.job.pawnKind, pawn.Faction));
            GenSpawn.Spawn(mim, cell, map);
            if (mim.connections == null)
            {
                mim.connections = new Pawn_ConnectionsTracker(mim);
            }
            mim.connections.ConnectTo(pawn);
            var devotion = (Hediff_MimDevotion)mim.health.AddHediff(RH_DefOf.RH_MimDevotion);
            devotion.master = pawn;
            bond.mim = mim;
            bond.respawnAtTick = -1;
            Messages.Message("RH_MimArrived".Translate(bond.job.LabelCap, pawn.LabelShortCap), mim, MessageTypeDefOf.PositiveEvent);
        }

        public void DebugForceMimRespawn()
        {
            foreach (var bond in mimBonds)
            {
                if (bond.mim == null)
                {
                    bond.respawnAtTick = 0;
                }
            }
        }

        // ===== Spellcasting: Vancian slots, rests, autocast =====

        private List<int> slotsExpended = new List<int>(new int[10]); // index = spell level 1..9
        private List<AbilityDef> autocastSpells = new List<AbilityDef>();
        public bool longResting;
        private int longRestProgress;
        private bool shortRestArmed = true;

        public const int LongRestDurationTicks = 30000; // 12 in-game hours of sleep

        public int MaxSlots(int spellLevel) => SpellUtility.MaxSlots(classDef?.casterProgression ?? CasterProgression.None, level, spellLevel);

        public int RemainingSlots(int spellLevel) =>
            spellLevel < 1 || spellLevel > 9 ? 0 : Mathf.Max(0, MaxSlots(spellLevel) - slotsExpended[spellLevel]);

        public bool TryExpendSlot(int spellLevel)
        {
            if (RemainingSlots(spellLevel) <= 0)
            {
                return false;
            }
            slotsExpended[spellLevel]++;
            return true;
        }

        public void RefreshAllSlots()
        {
            for (int i = 0; i < slotsExpended.Count; i++)
            {
                slotsExpended[i] = 0;
            }
        }

        /// <summary>Short rest benefit: recover one expended slot of the lowest expended level.</summary>
        public bool RestoreLowestExpendedSlot()
        {
            for (int lvl = 1; lvl <= 9; lvl++)
            {
                if (slotsExpended[lvl] > 0 && MaxSlots(lvl) > 0)
                {
                    slotsExpended[lvl]--;
                    return true;
                }
            }
            return false;
        }

        public bool AutocastEnabled(AbilityDef def) => autocastSpells.Contains(def);

        public void SetAutocast(AbilityDef def, bool on)
        {
            if (on && !autocastSpells.Contains(def))
            {
                autocastSpells.Add(def);
            }
            else if (!on)
            {
                autocastSpells.Remove(def);
            }
        }

        public void Notify_SpellGranted(AbilityDef def)
        {
            // Hostile cantrips autocast by default; self/utility spells stay manual.
            if (SpellUtility.IsCantrip(def) && def.hostile)
            {
                SetAutocast(def, true);
            }
        }

        private void TickRests(int delta)
        {
            bool asleep = !pawn.Dead && !pawn.Awake();
            var rest = pawn.needs?.rest;
            if (longResting)
            {
                if (asleep)
                {
                    longRestProgress += delta;
                    if (longRestProgress >= LongRestDurationTicks)
                    {
                        longResting = false;
                        longRestProgress = 0;
                        RefreshAllSlots();
                        // Long rest also completes the short-rest benefit cycle.
                        shortRestArmed = false;
                        if (PawnUtility.ShouldSendNotificationAbout(pawn))
                        {
                            Messages.Message("RH_LongRestComplete".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent);
                        }
                    }
                }
                return;
            }
            if (rest == null)
            {
                return;
            }
            if (shortRestArmed && asleep && rest.CurLevelPercentage >= 0.98f)
            {
                shortRestArmed = false;
                if (RestoreLowestExpendedSlot() && PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message("RH_ShortRest".Translate(pawn.LabelShortCap), pawn, MessageTypeDefOf.PositiveEvent);
                }
            }
            else if (!shortRestArmed && rest.CurLevelPercentage < 0.7f)
            {
                shortRestArmed = true;
            }
        }

        private void TickAutocast()
        {
            if (!pawn.Spawned || pawn.Downed || pawn.Dead || !pawn.Drafted || pawn.abilities == null)
            {
                return;
            }
            if (pawn.CurJob?.ability != null)
            {
                return; // already casting
            }
            foreach (var ability in pawn.abilities.abilities)
            {
                if (!(ability is Ability_Spell spell) || !AutocastEnabled(spell.def) || !spell.def.hostile)
                {
                    continue;
                }
                if (spell.GizmoDisabled(out _))
                {
                    continue;
                }
                float range = spell.verb?.verbProps?.range ?? 0f;
                if (range <= 0f)
                {
                    continue;
                }
                var target = FindAutocastTarget(range);
                if (target != null)
                {
                    spell.QueueCastingJob(target, target);
                    return;
                }
            }
        }

        private Pawn FindAutocastTarget(float range)
        {
            Pawn best = null;
            float bestDist = float.MaxValue;
            foreach (var other in pawn.Map.mapPawns.AllPawnsSpawned)
            {
                if (other.Dead || other.Downed || !other.HostileTo(pawn))
                {
                    continue;
                }
                float dist = pawn.Position.DistanceTo(other.Position);
                if (dist <= range && dist < bestDist && GenSight.LineOfSight(pawn.Position, other.Position, pawn.Map, skipFirstCell: true))
                {
                    best = other;
                    bestDist = dist;
                }
            }
            return best;
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var gizmo in base.GetGizmos())
            {
                yield return gizmo;
            }
            if (!pawn.IsColonistPlayerControlled)
            {
                yield break;
            }
            if (classDef != null && classDef.casterProgression != CasterProgression.None)
            {
                yield return new Command_Toggle
                {
                    defaultLabel = "RH_LongRestGizmo".Translate(),
                    defaultDesc = "RH_LongRestGizmoDesc".Translate(),
                    icon = RH_Tex.LongRest,
                    isActive = () => longResting,
                    toggleAction = () =>
                    {
                        longResting = !longResting;
                        if (!longResting)
                        {
                            longRestProgress = 0;
                        }
                    }
                };
            }
            if (pawn.abilities != null)
            {
                foreach (var ability in pawn.abilities.abilities)
                {
                    if (!(ability is Ability_Spell spell))
                    {
                        continue;
                    }
                    var def = spell.def;
                    yield return new Command_Toggle
                    {
                        defaultLabel = "RH_AutocastGizmo".Translate(def.label),
                        defaultDesc = "RH_AutocastGizmoDesc".Translate(def.label),
                        icon = def.uiIcon,
                        isActive = () => AutocastEnabled(def),
                        toggleAction = () => SetAutocast(def, !AutocastEnabled(def))
                    };
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
            // Hook for future bookkeeping (mim panic event, revival quest seeding).
        }

        /// <summary>Set level directly (enemy hero generation, debug): applies grants + vestment tier.</summary>
        public void SetLevelDirect(int newLevel)
        {
            level = Mathf.Clamp(newLevel, 1, classDef?.maxLevel ?? 20);
            xp = 0f;
            ApplyGrants();
            Vestment?.SetTierForLevel(level);
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
                Vestment?.SetTierForLevel(level);
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
                        Notify_SpellGranted(abilityDef);
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
            Scribe_Collections.Look(ref mimBonds, "mimBonds", LookMode.Deep);
            Scribe_Collections.Look(ref slotsExpended, "slotsExpended", LookMode.Value);
            Scribe_Collections.Look(ref autocastSpells, "autocastSpells", LookMode.Def);
            Scribe_Values.Look(ref longResting, "longResting");
            Scribe_Values.Look(ref longRestProgress, "longRestProgress");
            Scribe_Values.Look(ref shortRestArmed, "shortRestArmed", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (mimBonds == null) mimBonds = new List<MimBond>();
                if (autocastSpells == null) autocastSpells = new List<AbilityDef>();
                if (slotsExpended == null || slotsExpended.Count != 10) slotsExpended = new List<int>(new int[10]);
            }
        }
    }

    public class MimBond : IExposable
    {
        public MimJobDef job;
        public Pawn mim;
        public int respawnAtTick = -1;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref job, "job");
            Scribe_References.Look(ref mim, "mim");
            Scribe_Values.Look(ref respawnAtTick, "respawnAtTick", -1);
        }
    }
}
