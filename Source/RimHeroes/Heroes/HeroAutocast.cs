using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Intent-classified autocast target resolver for drafted heroes, modeled on vanilla ability
    /// autocast and the magic-mod pattern (VPE / RoM): infer an ability's intent from its effect
    /// comps, then pick a sensible target for that intent. Keeps the brawl test driver
    /// (GameComponent_BrawlSpike) entirely separate by design.
    /// </summary>
    public static class HeroAutocast
    {
        public enum Intent
        {
            HostileSingle,   // direct damage / hostile projectile / hostile verb -> nearest foe
            EnemyAoE,        // explosion / enemy zone / turn undead -> cell over most foes
            Heal,            // cure wounds -> most-wounded ally (incl. self)
            AllySupport,     // ally buff / ally zone / cleanse -> self or cell over most allies
            Summon,          // familiar -> self, only if not already summoned
            Revivify,        // resurrect a fresh ally corpse
            SelfBuff,        // non-spell self buff -> self
            None             // not autocastable
        }

        // ===== Tuning =====

        // Don't waste a heal slot topping someone off: only fire if an ally is hurt past this.
        private const float HealMissingHealthThreshold = 0.25f;

        /// <summary>Classify what an ability is trying to do from its effect-comp properties.</summary>
        public static Intent ClassifyIntent(Ability ability) => ClassifyIntent(ability?.def);

        /// <summary>Classify intent straight from a def (used at grant time before an Ability exists).</summary>
        public static Intent ClassifyIntent(AbilityDef def)
        {
            if (def == null)
            {
                return Intent.None;
            }
            var comps = def.comps;
            if (comps != null)
            {
                foreach (var c in comps)
                {
                    switch (c)
                    {
                        case CompProperties_AbilityHeal _:
                            return Intent.Heal;
                        case CompProperties_AbilityRevivify _:
                            return Intent.Revivify;
                        case CompProperties_AbilitySummon _:
                            return Intent.Summon;
                        case CompProperties_AbilityExplosion _:
                            return Intent.EnemyAoE;
                        case CompProperties_AbilityTurnUndead _:
                            return Intent.EnemyAoE;
                        case CompProperties_AbilityCleanse _:
                            return Intent.AllySupport;
                        case CompProperties_AbilityZoneHediff zone:
                            return zone.targets == ZoneTargets.Enemies ? Intent.EnemyAoE : Intent.AllySupport;
                        case CompProperties_AbilityDamageTarget _:
                            return Intent.HostileSingle;
                    }
                }
            }
            // No recognized RimHeroes effect comp: fall back to verb/hostility shape.
            if (def.hostile)
            {
                return Intent.HostileSingle;
            }
            return Intent.SelfBuff;
        }

        /// <summary>
        /// Resolve a cast target for the ability given the caster's intent. Returns false when there
        /// is nothing worth casting on (skips topping-off heals, re-buffing, re-summoning, etc.).
        /// </summary>
        public static bool TryResolveTarget(Pawn caster, Ability ability, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (caster?.Map == null || ability == null)
            {
                return false;
            }
            float range = ability.verb?.verbProps?.range ?? 0f;
            switch (ClassifyIntent(ability))
            {
                case Intent.HostileSingle:
                    return TryHostileSingle(caster, ability, range, out target);
                case Intent.EnemyAoE:
                    return TryEnemyAoE(caster, ability, range, out target);
                case Intent.Heal:
                    return TryHeal(caster, ability, range, out target);
                case Intent.AllySupport:
                    return TryAllySupport(caster, ability, range, out target);
                case Intent.Summon:
                    return TrySummon(caster, ability, out target);
                case Intent.Revivify:
                    return TryRevivify(caster, ability, range, out target);
                case Intent.SelfBuff:
                    return TrySelfBuff(caster, ability, out target);
                default:
                    return false;
            }
        }

        /// <summary>
        /// True when the pawn is actively fighting: has a live enemy target, or any hostile pawn is
        /// within sight. Gates AI-hero autocast so raiders only spend their kit in a real fight.
        /// </summary>
        public static bool InCombat(Pawn pawn)
        {
            if (pawn?.Map == null)
            {
                return false;
            }
            if (pawn.mindState?.enemyTarget is Pawn et && !et.Dead && !et.Downed)
            {
                return true;
            }
            return NearestHostileInRange(pawn, 40f, requireLineOfSight: false) != null;
        }

        // ===== Hostile single-target / projectile / hostile verb =====

        private static bool TryHostileSingle(Pawn caster, Ability ability, float range, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (range <= 0f)
            {
                return false;
            }
            var foe = NearestHostileInRange(caster, range, requireLineOfSight: true);
            if (foe == null)
            {
                return false;
            }
            target = foe;
            return CanApply(ability, target);
        }

        // ===== Enemy AoE: cell covering the most hostiles =====

        private static bool TryEnemyAoE(Pawn caster, Ability ability, float range, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (range <= 0f)
            {
                return false;
            }
            float aoe = AoeRadius(ability);
            IntVec3 bestCell = IntVec3.Invalid;
            int bestHits = 0;
            // Use the foes themselves as candidate centers: cheap and always covers at least one.
            foreach (var foe in HostilePawns(caster))
            {
                float distToCaster = caster.Position.DistanceTo(foe.Position);
                if (distToCaster > range)
                {
                    continue;
                }
                if (!GenSight.LineOfSight(caster.Position, foe.Position, caster.Map, skipFirstCell: true))
                {
                    continue;
                }
                int hits = CountHostilesAround(caster, foe.Position, aoe);
                if (hits > bestHits)
                {
                    bestHits = hits;
                    bestCell = foe.Position;
                }
            }
            if (!bestCell.IsValid)
            {
                return false;
            }
            target = new LocalTargetInfo(bestCell);
            return true;
        }

        // ===== Heal: most-wounded ally in range, only if hurt past threshold =====

        private static bool TryHeal(Pawn caster, Ability ability, float range, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (range <= 0f)
            {
                range = 9999f; // some heals are self/touch with no verb range; allow self/nearby
            }
            Pawn best = null;
            float worst = 1f - HealMissingHealthThreshold;
            foreach (var ally in AlliesInRange(caster, range, includeSelf: true))
            {
                float health = ally.health?.summaryHealth?.SummaryHealthPercent ?? 1f;
                if (health < worst)
                {
                    worst = health;
                    best = ally;
                }
            }
            if (best == null)
            {
                return false;
            }
            target = best;
            return CanApply(ability, target);
        }

        // ===== Ally support: buff / cleanse / ally zone. Skip if already buffed. =====

        private static bool TryAllySupport(Pawn caster, Ability ability, float range, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            var zone = FindCompProps<CompProperties_AbilityZoneHediff>(ability);
            var cleanse = FindCompProps<CompProperties_AbilityCleanse>(ability);

            if (range <= 0f)
            {
                range = 9999f;
            }

            // Zone ally buff: aim at the cell covering the most allies who still need it.
            if (zone != null && zone.hediff != null)
            {
                IntVec3 bestCell = IntVec3.Invalid;
                int bestCount = 0;
                foreach (var ally in AlliesInRange(caster, range, includeSelf: true))
                {
                    int count = AlliesNeedingBuffAround(caster, ally.Position, zone.radius, zone.hediff);
                    if (count > bestCount)
                    {
                        bestCount = count;
                        bestCell = ally.Position;
                    }
                }
                if (bestCount <= 0)
                {
                    return false; // everyone in reach already has it
                }
                target = new LocalTargetInfo(bestCell);
                return true;
            }

            // Cleanse: find an ally actually carrying a removable affliction.
            if (cleanse != null)
            {
                foreach (var ally in AlliesInRange(caster, range, includeSelf: true))
                {
                    if (HasCleansable(ally, cleanse) && CanApply(ability, ally))
                    {
                        target = ally;
                        return true;
                    }
                }
                return false;
            }

            // Single-target ally buff (rare): cast on self if not already affected.
            target = caster;
            return CanApply(ability, target);
        }

        // ===== Summon: self, only if the familiar is not already alive =====

        private static bool TrySummon(Pawn caster, Ability ability, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            var props = FindCompProps<CompProperties_AbilitySummon>(ability);
            if (props?.pawnKind == null || caster.Map == null)
            {
                return false;
            }
            bool alreadyOut = caster.Map.mapPawns.SpawnedPawnsInFaction(caster.Faction)
                .Any(p => p.kindDef == props.pawnKind && !p.Dead);
            if (alreadyOut)
            {
                return false;
            }
            target = caster;
            return true;
        }

        // ===== Revivify: a valid fresh ally corpse (reuse the comp's own Valid) =====

        private static bool TryRevivify(Pawn caster, Ability ability, float range, out LocalTargetInfo target)
        {
            target = LocalTargetInfo.Invalid;
            if (caster.Map == null)
            {
                return false;
            }
            if (range <= 0f)
            {
                range = 9999f;
            }
            var comp = ability.EffectComps?.OfType<CompAbilityEffect_Revivify>().FirstOrDefault();
            Corpse best = null;
            float bestDist = float.MaxValue;
            foreach (var thing in caster.Map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
            {
                if (!(thing is Corpse corpse))
                {
                    continue;
                }
                var inner = corpse.InnerPawn;
                if (inner == null || inner.HostileTo(caster))
                {
                    continue; // only raise allies/colonists, not enemies
                }
                float dist = caster.Position.DistanceTo(corpse.Position);
                if (dist > range || dist >= bestDist)
                {
                    continue;
                }
                var t = new LocalTargetInfo(corpse);
                bool valid = comp != null ? comp.Valid(t) : (corpse.GetRotStage() == RotStage.Fresh);
                if (valid)
                {
                    best = corpse;
                    bestDist = dist;
                }
            }
            if (best == null)
            {
                return false;
            }
            target = new LocalTargetInfo(best);
            return true;
        }

        // ===== Non-spell self buff =====

        private static bool TrySelfBuff(Pawn caster, Ability ability, out LocalTargetInfo target)
        {
            target = caster;
            return CanApply(ability, target);
        }

        // ===== Shared helpers =====

        private static IEnumerable<Pawn> HostilePawns(Pawn caster)
        {
            foreach (var other in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (!other.Dead && !other.Downed && other.HostileTo(caster))
                {
                    yield return other;
                }
            }
        }

        private static Pawn NearestHostileInRange(Pawn caster, float range, bool requireLineOfSight)
        {
            Pawn best = null;
            float bestDist = float.MaxValue;
            foreach (var foe in HostilePawns(caster))
            {
                float dist = caster.Position.DistanceTo(foe.Position);
                if (dist > range || dist >= bestDist)
                {
                    continue;
                }
                if (requireLineOfSight && !GenSight.LineOfSight(caster.Position, foe.Position, caster.Map, skipFirstCell: true))
                {
                    continue;
                }
                best = foe;
                bestDist = dist;
            }
            return best;
        }

        private static int CountHostilesAround(Pawn caster, IntVec3 cell, float radius)
        {
            int n = 0;
            foreach (var p in GenRadial.RadialDistinctThingsAround(cell, caster.Map, radius, true).OfType<Pawn>())
            {
                if (!p.Dead && p.HostileTo(caster))
                {
                    n++;
                }
            }
            return n;
        }

        private static IEnumerable<Pawn> AlliesInRange(Pawn caster, float range, bool includeSelf)
        {
            foreach (var other in caster.Map.mapPawns.AllPawnsSpawned)
            {
                if (other.Dead || other.Downed)
                {
                    continue;
                }
                if (other == caster)
                {
                    if (includeSelf)
                    {
                        yield return other;
                    }
                    continue;
                }
                if (other.HostileTo(caster) || other.RaceProps == null || !other.RaceProps.Humanlike)
                {
                    continue; // buff/heal the party, not wild animals
                }
                if (caster.Position.DistanceTo(other.Position) <= range)
                {
                    yield return other;
                }
            }
        }

        private static int AlliesNeedingBuffAround(Pawn caster, IntVec3 cell, float radius, HediffDef buff)
        {
            int n = 0;
            foreach (var p in GenRadial.RadialDistinctThingsAround(cell, caster.Map, radius, true).OfType<Pawn>())
            {
                if (p.Dead || p.HostileTo(caster))
                {
                    continue;
                }
                if (p != caster && (p.RaceProps == null || !p.RaceProps.Humanlike))
                {
                    continue;
                }
                if (buff == null || !p.health.hediffSet.HasHediff(buff))
                {
                    n++;
                }
            }
            return n;
        }

        private static bool HasCleansable(Pawn ally, CompProperties_AbilityCleanse cleanse)
        {
            if (ally?.health == null)
            {
                return false;
            }
            foreach (var h in ally.health.hediffSet.hediffs)
            {
                if ((cleanse.hediffs != null && cleanse.hediffs.Contains(h.def))
                    || (cleanse.removeBadDiseases && h.def.makesSickThought))
                {
                    return true;
                }
            }
            return false;
        }

        private static float AoeRadius(Ability ability)
        {
            var explosion = FindCompProps<CompProperties_AbilityExplosion>(ability);
            if (explosion != null)
            {
                return explosion.radius;
            }
            var turn = FindCompProps<CompProperties_AbilityTurnUndead>(ability);
            if (turn != null)
            {
                return turn.radius;
            }
            var zone = FindCompProps<CompProperties_AbilityZoneHediff>(ability);
            if (zone != null)
            {
                return zone.radius;
            }
            return 2.9f;
        }

        private static T FindCompProps<T>(Ability ability) where T : CompProperties_AbilityEffect
        {
            var comps = ability?.def?.comps;
            if (comps == null)
            {
                return null;
            }
            return comps.OfType<T>().FirstOrDefault();
        }

        /// <summary>Reuse a comp's own CanApplyOn validity where it exists; default to allowing.</summary>
        private static bool CanApply(Ability ability, LocalTargetInfo target)
        {
            var comps = ability.EffectComps;
            if (comps == null)
            {
                return true;
            }
            foreach (var comp in comps)
            {
                if (!comp.CanApplyOn(target, target))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
