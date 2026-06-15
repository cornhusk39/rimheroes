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
        public bool capstoneGranted; // L20 "heroic trial" reward fired once
        private List<int> resolvedChoiceLevels = new List<int>(); // trait/feat level-up picks already made
        private List<int> resolvedBonusFeatLevels = new List<int>(); // class bonus-feat picks (separate: may share a level with a trait pick)
        private List<FeatDef> takenFeats = new List<FeatDef>();

        public bool IsBonusFeatResolved(int lvl) => resolvedBonusFeatLevels.Contains(lvl);
        public void MarkBonusFeatResolved(int lvl) { if (!resolvedBonusFeatLevels.Contains(lvl)) resolvedBonusFeatLevels.Add(lvl); }

        public List<FeatDef> TakenFeats => takenFeats;
        public bool IsChoiceResolved(int lvl) => resolvedChoiceLevels.Contains(lvl);
        public void MarkChoiceResolved(int lvl) { if (!resolvedChoiceLevels.Contains(lvl)) resolvedChoiceLevels.Add(lvl); }
        public void AddTakenFeat(FeatDef f) { if (f != null && !takenFeats.Contains(f)) takenFeats.Add(f); }

        // ===== Class features (displayed on the single class-named hediff) =====

        public FighterStyle fighterStyle = FighterStyle.None; // Fighter's L1 Fighting Style pick
        public FighterStyle FightingStyle => fighterStyle;
        public void SetFightingStyle(FighterStyle s) { fighterStyle = s; }

        public FavoredEnemy favoredEnemy = FavoredEnemy.None; // Ranger's L1 Favored Enemy pick
        public void SetFavoredEnemy(FavoredEnemy e) { favoredEnemy = e; }

        public PactBoon pactBoon = PactBoon.None; // Warlock's L3 Pact Boon pick
        public void SetPactBoon(PactBoon b) { pactBoon = b; }

        // ===== Known-caster spell learning (5e: limited spells known, player-picked) =====

        private List<AbilityDef> learnedSpells = new List<AbilityDef>();
        public List<AbilityDef> LearnedSpells => learnedSpells;

        /// <summary>True for known casters that learn a capped set (Sorcerer/Bard/Ranger/Warlock);
        /// prepared casters (Cleric/Druid/Wizard/Paladin) know all their granted spells instead.</summary>
        public bool LearnsLimitedSpells =>
            classDef != null && !classDef.preparesSpells
            && classDef.casterProgression != CasterProgression.None
            && classDef.spellsKnownByLevel != null && classDef.spellsKnownByLevel.Count > 0;

        public int CantripsAllowed =>
            classDef == null ? 0 : classDef.cantripsKnownBase + (level >= 4 ? 1 : 0) + (level >= 10 ? 1 : 0);

        public int LeveledSpellsAllowed
        {
            get
            {
                var t = classDef?.spellsKnownByLevel;
                if (t == null || t.Count == 0) return 0;
                return t[Mathf.Clamp(level - 1, 0, t.Count - 1)];
            }
        }

        public int KnownCantrips => learnedSpells.Count(s => s != null && s.level == 0);
        public int KnownLeveledSpells => learnedSpells.Count(s => s != null && s.level > 0);

        public bool HasLearned(AbilityDef def) => learnedSpells.Contains(def);

        public void LearnSpell(AbilityDef def)
        {
            if (def == null || learnedSpells.Contains(def)) return;
            learnedSpells.Add(def);
            pawn.abilities?.GainAbility(def);
            Notify_SpellGranted(def);
        }

        /// <summary>All spells (cantrips + leveled) this class can learn at the current level,
        /// drawn from its levelGrants pool. Used by the learn UI / auto-learn.</summary>
        public IEnumerable<AbilityDef> LearnableSpellPool()
        {
            if (classDef?.levelGrants == null) yield break;
            foreach (var grant in classDef.levelGrants)
            {
                if (grant.level > level || grant.abilities == null) continue;
                foreach (var a in grant.abilities)
                {
                    if (a != null && SpellUtility.IsSpell(a)) yield return a;
                }
            }
        }

        /// <summary>Bonus to death saving throws from class features (Fighter's Indomitable).</summary>
        public int DeathSaveBonus => ClassFeatures.DeathSaveBonus(this);

        /// <summary>Ensures the class-features summary hediff (labeled with the class name) is present.</summary>
        public void EnsureClassFeaturesHediff()
        {
            if (pawn?.health == null || RH_DefOf.RH_ClassFeatures == null) return;
            if (!pawn.health.hediffSet.HasHediff(RH_DefOf.RH_ClassFeatures))
            {
                pawn.health.AddHediff(RH_DefOf.RH_ClassFeatures);
            }
        }

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
            HeroUtility.NormalizeHeroBody(pawn);
            EnsureClassFeaturesHediff();
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
        private List<AbilityDef> preparedSpells = new List<AbilityDef>(); // prepared casters' active leveled spells
        private int prepareWindowEndTick = -1; // absolute tick; spells can be swapped until then
        public bool longResting;
        private int longRestProgress;
        private bool shortRestArmed = true;

        public const int LongRestDurationTicks = 30000; // 12 in-game hours of sleep
        public const int PrepareWindowTicks = 15000;    // 6 in-game hours to swap spells after a long rest

        // ===== Wizard signature mechanics (Arcane Recovery / Spell Mastery / Signature Spells) =====

        public const int SpellMasteryLevel = 18;
        public const int SignatureSpellsLevel = 20;

        private bool arcaneRecoveryReady = true;        // recharges on a long rest
        private AbilityDef masteryLow, masteryMid;      // L18: one lvl-1 + one lvl-2 spell cast at-will
        private AbilityDef signatureA, signatureB;      // L20: two lvl-3 spells, free once per long rest
        private bool signatureAReady = true, signatureBReady = true;

        public bool HasArcaneRecovery => classDef?.arcaneRecovery == true;
        public bool HasSpellMastery => classDef?.spellMastery == true && level >= SpellMasteryLevel;
        public bool HasSignatureSpells => classDef?.signatureSpells == true && level >= SignatureSpellsLevel;

        public AbilityDef MasteryLow => masteryLow;
        public AbilityDef MasteryMid => masteryMid;
        public AbilityDef SignatureA => signatureA;
        public AbilityDef SignatureB => signatureB;

        /// <summary>A mastered spell is cast at-will (Spell Mastery): no slot, ignores the prepared gate.</summary>
        public bool IsMastered(AbilityDef def) =>
            HasSpellMastery && def != null && (def == masteryLow || def == masteryMid);

        public bool IsSignature(AbilityDef def) =>
            HasSignatureSpells && def != null && (def == signatureA || def == signatureB);

        /// <summary>True if this signature spell still has its free per-long-rest cast available.</summary>
        public bool SignatureChargeReady(AbilityDef def) =>
            IsSignature(def) && ((def == signatureA && signatureAReady) || (def == signatureB && signatureBReady));

        /// <summary>Spend a signature free cast if one remains. Returns true if a charge was consumed.</summary>
        public bool TryConsumeSignatureCharge(AbilityDef def)
        {
            if (!SignatureChargeReady(def)) return false;
            if (def == signatureA) signatureAReady = false;
            else if (def == signatureB) signatureBReady = false;
            return true;
        }

        public void SetMastery(bool mid, AbilityDef def) { if (mid) masteryMid = def; else masteryLow = def; }
        public void SetSignature(bool second, AbilityDef def) { if (second) signatureB = def; else signatureA = def; }

        /// <summary>Known leveled spells of an exact spell level (for the mastery/signature pickers).</summary>
        public IEnumerable<AbilityDef> KnownSpellsOfLevel(int spellLevel) =>
            (pawn.abilities?.abilities ?? Enumerable.Empty<Ability>())
                .Where(a => a is Ability_Spell && a.def.level == spellLevel)
                .Select(a => a.def)
                .Distinct();

        /// <summary>Idempotently assign sensible default mastery/signature picks so the mechanic works
        /// for every wizard (player customizes via gizmos). Only fills empty slots.</summary>
        private void EnsureSignaturePicks()
        {
            if (HasSpellMastery)
            {
                if (masteryLow == null) masteryLow = KnownSpellsOfLevel(1).FirstOrDefault();
                if (masteryMid == null) masteryMid = KnownSpellsOfLevel(2).FirstOrDefault();
            }
            if (HasSignatureSpells)
            {
                var l3 = KnownSpellsOfLevel(3).ToList();
                if (signatureA == null) signatureA = l3.FirstOrDefault();
                if (signatureB == null) signatureB = l3.FirstOrDefault(d => d != signatureA);
            }
        }

        /// <summary>Arcane Recovery: on a short rest, restore expended slots totaling up to ceil(level/2)
        /// spell-levels (none above 5th), highest first. Once per long rest.</summary>
        private int DoArcaneRecovery()
        {
            int budget = Mathf.CeilToInt(level / 2f);
            int restored = 0;
            for (int lvl = 5; lvl >= 1 && budget > 0; lvl--)
            {
                while (budget >= lvl && slotsExpended[lvl] > 0 && MaxSlots(lvl) > 0)
                {
                    slotsExpended[lvl]--;
                    budget -= lvl;
                    restored++;
                }
            }
            return restored;
        }

        // ===== Prepared casting (Wizard/Cleric/Druid/Paladin): ready a subset of known leveled spells =====

        public bool PreparesSpells => classDef?.preparesSpells == true;

        public bool PrepareWindowOpen => prepareWindowEndTick > 0 && Find.TickManager.TicksGame < prepareWindowEndTick;

        /// <summary>How many leveled (non-cantrip) spells this hero may have prepared at once.</summary>
        public int PreparedMax => Mathf.Max(1, 3 + level / 2);

        public int PreparedLeveledCount => preparedSpells.Count(s => s != null && s.level > 0);

        public bool IsPrepared(AbilityDef def) => preparedSpells.Contains(def);

        /// <summary>Cantrips and known-caster spells are always castable; prepared casters gate leveled spells.</summary>
        public bool CanCastSpell(AbilityDef def)
        {
            if (def == null) return false;
            if (!PreparesSpells || def.level <= 0) return true;
            return preparedSpells.Contains(def);
        }

        public bool SetPrepared(AbilityDef def, bool on)
        {
            if (def == null || def.level <= 0) return false;
            if (on)
            {
                if (preparedSpells.Contains(def)) return true;
                if (PreparedLeveledCount >= PreparedMax) return false; // at capacity
                preparedSpells.Add(def);
            }
            else
            {
                preparedSpells.Remove(def);
            }
            return true;
        }

        /// <summary>Auto-ready a newly-learned leveled spell if there is still room (sensible default loadout).</summary>
        private void AutoPrepare(AbilityDef def)
        {
            if (PreparesSpells && def != null && def.level > 0
                && !preparedSpells.Contains(def) && PreparedLeveledCount < PreparedMax)
            {
                preparedSpells.Add(def);
            }
        }

        public void OpenPrepareWindow()
        {
            prepareWindowEndTick = Find.TickManager.TicksGame + PrepareWindowTicks;
        }

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
            // A long rest also recharges the per-rest wizard mechanics.
            arcaneRecoveryReady = true;
            signatureAReady = true;
            signatureBReady = true;
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
            AutoPrepare(def); // prepared casters start with a sensible loadout
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
                        if (PreparesSpells)
                        {
                            OpenPrepareWindow();
                            if (PawnUtility.ShouldSendNotificationAbout(pawn))
                            {
                                Messages.Message(pawn.LabelShortCap + " may prepare spells for the next 6 hours.",
                                    pawn, MessageTypeDefOf.NeutralEvent);
                            }
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
                bool any = RestoreLowestExpendedSlot();
                // Arcane Recovery rides the short rest: a wizard reclaims a burst of slots, once per long rest.
                if (HasArcaneRecovery && arcaneRecoveryReady)
                {
                    arcaneRecoveryReady = false;
                    int recovered = DoArcaneRecovery();
                    if (recovered > 0)
                    {
                        any = true;
                        if (PawnUtility.ShouldSendNotificationAbout(pawn))
                        {
                            Messages.Message(pawn.LabelShortCap + " reclaims arcane energy on resting (Arcane Recovery: " +
                                recovered + " slot" + (recovered == 1 ? "" : "s") + ").", pawn, MessageTypeDefOf.PositiveEvent);
                        }
                    }
                }
                if (any && !HasArcaneRecovery && PawnUtility.ShouldSendNotificationAbout(pawn))
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
                if (PreparesSpells)
                {
                    string suffix = PrepareWindowOpen ? "" : " (locked - rest to change)";
                    yield return new Command_Action
                    {
                        defaultLabel = "Prepare spells" + (PrepareWindowOpen ? "" : " *"),
                        defaultDesc = $"Ready which leveled spells {pawn.LabelShort} can cast ({PreparedLeveledCount}/{PreparedMax}). " +
                                      "Swappable for 6 hours after a long rest." + suffix,
                        icon = RH_Tex.LongRest,
                        action = () => Find.WindowStack.Add(new Dialog_PrepareSpells(this))
                    };
                }
                if (HasSpellMastery)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "Spell Mastery",
                        defaultDesc = "Choose one level-1 and one level-2 spell to cast at will, with no spell slot.\n" +
                                      "Current: " + (masteryLow?.LabelCap ?? "(none)") + " / " + (masteryMid?.LabelCap ?? "(none)"),
                        icon = (masteryLow ?? masteryMid)?.uiIcon ?? RH_Tex.LongRest,
                        action = () => Find.WindowStack.Add(new Dialog_PickSpell(this, 1, "Spell Mastery: level-1 spell",
                            masteryLow, null, picked =>
                            {
                                masteryLow = picked;
                                Find.WindowStack.Add(new Dialog_PickSpell(this, 2, "Spell Mastery: level-2 spell",
                                    masteryMid, null, second => masteryMid = second));
                            }))
                    };
                }
                if (HasSignatureSpells)
                {
                    yield return new Command_Action
                    {
                        defaultLabel = "Signature Spells",
                        defaultDesc = "Choose two level-3 spells. Each can be cast free once per long rest.\n" +
                                      "Current: " + (signatureA?.LabelCap ?? "(none)") + " / " + (signatureB?.LabelCap ?? "(none)"),
                        icon = (signatureA ?? signatureB)?.uiIcon ?? RH_Tex.LongRest,
                        action = () => Find.WindowStack.Add(new Dialog_PickSpell(this, 3, "Signature spell (1 of 2)",
                            signatureA, signatureB, picked =>
                            {
                                signatureA = picked;
                                Find.WindowStack.Add(new Dialog_PickSpell(this, 3, "Signature spell (2 of 2)",
                                    signatureB, signatureA, second => signatureB = second));
                            }))
                    };
                }
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
            TryGrantCapstone();
            HeroChoices.CheckLevelChoices(this, allowDialog: false); // direct set = auto-pick (gen/debug)
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
                TryGrantCapstone();
                HeroChoices.CheckLevelChoices(this);
            }
            if (AtMaxLevel)
            {
                xp = 0f;
            }
        }

        /// <summary>
        /// "Heroic trial" capstone reward (dev stub for the real quest): when a player hero hits max
        /// level, drop their class's legendary capstone weapon at their feet, once.
        /// </summary>
        private void TryGrantCapstone()
        {
            if (capstoneGranted || classDef == null || level < (classDef.maxLevel))
            {
                return;
            }
            if (pawn == null || !pawn.Spawned || pawn.Map == null || pawn.Faction != Faction.OfPlayer)
            {
                return;
            }
            string weaponDefName = "RH_Weapon_" + classDef.defName.Substring("RH_".Length) + "_T5";
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDefName);
            if (def == null)
            {
                return;
            }
            capstoneGranted = true;
            var weapon = (ThingWithComps)ThingMaker.MakeThing(def);
            weapon.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Legendary, ArtGenerationContext.Colony);
            GenPlace.TryPlaceThing(weapon, pawn.Position, pawn.Map, ThingPlaceMode.Near);
            Log.Message($"[RimHeroes] capstone granted to {pawn.LabelShort}: {def.defName} (Legendary)");
            Find.LetterStack.ReceiveLetter(
                "Heroic capstone",
                pawn.LabelShortCap + " has reached the height of their " + classDef.label +
                " legend. Their capstone weapon — the " + def.label + " — has manifested at their side.",
                LetterDefOf.PositiveEvent, new TargetInfo(weapon.PositionHeld, pawn.Map));
        }

        /// <summary>Idempotent: applies every grant at or below the current level. Known casters skip
        /// auto-granting spells (they learn a capped set via picks); everything else always grants.</summary>
        private void ApplyGrants()
        {
            if (classDef == null)
            {
                return;
            }
            bool limited = LearnsLimitedSpells;
            if (classDef.levelGrants != null)
            {
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
                            if (limited && SpellUtility.IsSpell(abilityDef))
                            {
                                continue; // known casters learn these via picks, not auto-grant
                            }
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
            if (limited)
            {
                foreach (var s in learnedSpells)
                {
                    if (s != null) { pawn.abilities?.GainAbility(s); Notify_SpellGranted(s); }
                }
            }
            EnsureSignaturePicks(); // fill default Spell Mastery / Signature Spell picks once unlocked
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref classDef, "classDef");
            Scribe_Values.Look(ref level, "level", 1);
            Scribe_Values.Look(ref xp, "xp");
            Scribe_Values.Look(ref capstoneGranted, "capstoneGranted");
            Scribe_Collections.Look(ref resolvedChoiceLevels, "resolvedChoiceLevels", LookMode.Value);
            Scribe_Collections.Look(ref resolvedBonusFeatLevels, "resolvedBonusFeatLevels", LookMode.Value);
            Scribe_Collections.Look(ref takenFeats, "takenFeats", LookMode.Def);
            Scribe_Values.Look(ref fighterStyle, "fighterStyle", FighterStyle.None);
            Scribe_Values.Look(ref favoredEnemy, "favoredEnemy", FavoredEnemy.None);
            Scribe_Values.Look(ref pactBoon, "pactBoon", PactBoon.None);
            Scribe_Collections.Look(ref learnedSpells, "learnedSpells", LookMode.Def);
            Scribe_Collections.Look(ref mimBonds, "mimBonds", LookMode.Deep);
            Scribe_Collections.Look(ref slotsExpended, "slotsExpended", LookMode.Value);
            Scribe_Collections.Look(ref autocastSpells, "autocastSpells", LookMode.Def);
            Scribe_Collections.Look(ref preparedSpells, "preparedSpells", LookMode.Def);
            Scribe_Values.Look(ref prepareWindowEndTick, "prepareWindowEndTick", -1);
            Scribe_Values.Look(ref longResting, "longResting");
            Scribe_Values.Look(ref longRestProgress, "longRestProgress");
            Scribe_Values.Look(ref shortRestArmed, "shortRestArmed", true);
            Scribe_Values.Look(ref arcaneRecoveryReady, "arcaneRecoveryReady", true);
            Scribe_Defs.Look(ref masteryLow, "masteryLow");
            Scribe_Defs.Look(ref masteryMid, "masteryMid");
            Scribe_Defs.Look(ref signatureA, "signatureA");
            Scribe_Defs.Look(ref signatureB, "signatureB");
            Scribe_Values.Look(ref signatureAReady, "signatureAReady", true);
            Scribe_Values.Look(ref signatureBReady, "signatureBReady", true);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (mimBonds == null) mimBonds = new List<MimBond>();
                if (autocastSpells == null) autocastSpells = new List<AbilityDef>();
                if (preparedSpells == null) preparedSpells = new List<AbilityDef>();
                if (resolvedChoiceLevels == null) resolvedChoiceLevels = new List<int>();
                if (resolvedBonusFeatLevels == null) resolvedBonusFeatLevels = new List<int>();
                if (learnedSpells == null) learnedSpells = new List<AbilityDef>();
                if (takenFeats == null) takenFeats = new List<FeatDef>();
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
