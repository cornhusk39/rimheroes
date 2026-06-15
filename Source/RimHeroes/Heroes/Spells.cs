using System.Linq;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    public enum CasterProgression
    {
        None,
        Full,
        Half // paladin/ranger
        // Pact (warlock) later; warlock uses Full for now
    }

    public static class SpellUtility
    {
        // 5e full-caster slot table: [classLevel-1][spellLevel-1], spell levels 1..9.
        private static readonly int[][] FullCaster =
        {
            new[] {2,0,0,0,0,0,0,0,0}, // 1
            new[] {3,0,0,0,0,0,0,0,0},
            new[] {4,2,0,0,0,0,0,0,0},
            new[] {4,3,0,0,0,0,0,0,0},
            new[] {4,3,2,0,0,0,0,0,0}, // 5
            new[] {4,3,3,0,0,0,0,0,0},
            new[] {4,3,3,1,0,0,0,0,0},
            new[] {4,3,3,2,0,0,0,0,0},
            new[] {4,3,3,3,1,0,0,0,0},
            new[] {4,3,3,3,2,0,0,0,0}, // 10
            new[] {4,3,3,3,2,1,0,0,0},
            new[] {4,3,3,3,2,1,0,0,0},
            new[] {4,3,3,3,2,1,1,0,0},
            new[] {4,3,3,3,2,1,1,0,0},
            new[] {4,3,3,3,2,1,1,1,0}, // 15
            new[] {4,3,3,3,2,1,1,1,0},
            new[] {4,3,3,3,2,1,1,1,1},
            new[] {4,3,3,3,3,1,1,1,1},
            new[] {4,3,3,3,3,2,1,1,1},
            new[] {4,3,3,3,3,2,2,1,1}, // 20
        };

        // 5e half-caster table (paladin/ranger), spell levels 1..5.
        private static readonly int[][] HalfCaster =
        {
            new[] {0,0,0,0,0}, // 1
            new[] {2,0,0,0,0},
            new[] {3,0,0,0,0},
            new[] {3,0,0,0,0},
            new[] {4,2,0,0,0}, // 5
            new[] {4,2,0,0,0},
            new[] {4,3,0,0,0},
            new[] {4,3,0,0,0},
            new[] {4,3,2,0,0},
            new[] {4,3,2,0,0}, // 10
            new[] {4,3,3,0,0},
            new[] {4,3,3,0,0},
            new[] {4,3,3,1,0},
            new[] {4,3,3,1,0},
            new[] {4,3,3,2,0}, // 15
            new[] {4,3,3,2,0},
            new[] {4,3,3,3,1},
            new[] {4,3,3,3,1},
            new[] {4,3,3,3,2},
            new[] {4,3,3,3,2}, // 20
        };

        public static int MaxSlots(CasterProgression progression, int classLevel, int spellLevel)
        {
            if (spellLevel < 1)
            {
                return 0;
            }
            classLevel = Mathf.Clamp(classLevel, 1, 20);
            switch (progression)
            {
                case CasterProgression.Full when spellLevel <= 9:
                    return FullCaster[classLevel - 1][spellLevel - 1];
                case CasterProgression.Half when spellLevel <= 5:
                    return HalfCaster[classLevel - 1][spellLevel - 1];
                default:
                    return 0;
            }
        }

        public static bool IsSpell(AbilityDef def) => typeof(Ability_Spell).IsAssignableFrom(def.abilityClass);

        public static bool IsCantrip(AbilityDef def) => IsSpell(def) && def.level == 0;
    }

    /// <summary>
    /// A Vancian spell: AbilityDef.level is the spell level (0 = cantrip, at-will). Leveled spells
    /// require and consume a slot of their level from the caster's Hediff_HeroLevels.
    /// </summary>
    public class Ability_Spell : Ability
    {
        public Ability_Spell() { }
        public Ability_Spell(Pawn pawn) : base(pawn) { }
        public Ability_Spell(Pawn pawn, AbilityDef def) : base(pawn, def) { }
        public Ability_Spell(Pawn pawn, Precept sourcePrecept) : base(pawn, sourcePrecept) { }
        public Ability_Spell(Pawn pawn, Precept sourcePrecept, AbilityDef def) : base(pawn, sourcePrecept, def) { }

        public int SpellLevel => def.level;

        private Hediff_HeroLevels Hero => HeroUtility.GetHeroHediff(pawn);

        // A wildshape ability is a free (level 0) ability whose GiveHediff effect grants a
        // Hediff_Wildshape. These must stay castable while shifted so a druid can switch forms;
        // the Beast Spells lock below would otherwise trap them in their current shape.
        private bool IsWildshapeAbility
        {
            get
            {
                if (def.level != 0 || def.comps == null)
                {
                    return false;
                }
                foreach (var comp in def.comps)
                {
                    if (comp is RimWorld.CompProperties_AbilityGiveHediff give
                        && give.hediffDef != null
                        && typeof(Hediff_Wildshape).IsAssignableFrom(give.hediffDef.hediffClass))
                    {
                        return true;
                    }
                }
                return false;
            }
        }

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason))
            {
                return true;
            }
            // Beast Spells: a wildshaped druid can't cast spells until level 18. Wildshape abilities
            // are exempt so a shifted druid can still cast another form to switch shapes.
            if (Hediff_Wildshape.IsShifted(pawn) && !IsWildshapeAbility)
            {
                var beastHero = Hero;
                if (beastHero == null || beastHero.level < 18)
                {
                    reason = "Can't cast spells in beast form until level 18 (Beast Spells).";
                    return true;
                }
            }
            if (SpellLevel > 0)
            {
                var hero = Hero;
                if (hero == null)
                {
                    reason = "RH_NotAHero".Translate();
                    return true;
                }
                // Spell Mastery (at-will) and a ready Signature charge bypass the prepared gate and slots.
                bool free = hero.IsMastered(def) || hero.SignatureChargeReady(def);
                if (!free)
                {
                    if (!hero.CanCastSpell(def))
                    {
                        reason = "This spell is not prepared. Ready it after a long rest.";
                        return true;
                    }
                    if (hero.RemainingSlots(SpellLevel) <= 0)
                    {
                        reason = "RH_NoSlots".Translate(SpellLevel);
                        return true;
                    }
                }
            }
            return false;
        }

        public override bool Activate(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (!TryConsumeSlot())
            {
                return false;
            }
            return base.Activate(target, dest);
        }

        public override bool Activate(GlobalTargetInfo target)
        {
            if (!TryConsumeSlot())
            {
                return false;
            }
            return base.Activate(target);
        }

        private bool TryConsumeSlot()
        {
            if (SpellLevel <= 0)
            {
                return true;
            }
            var hero = Hero;
            if (hero == null)
            {
                return false;
            }
            if (hero.IsMastered(def))
            {
                return true; // Spell Mastery: at-will, no slot
            }
            if (hero.TryConsumeSignatureCharge(def))
            {
                return true; // Signature Spell: free once per long rest
            }
            return hero.TryExpendSlot(SpellLevel);
        }
    }

    /// <summary>
    /// Auto-hit direct damage (Magic Missile-style). Scales with Spell Power. Optional extras:
    /// lifesteal back to the caster, a bonus multiplier vs already-wounded targets (Toll the Dead),
    /// and a hediff applied on hit (Chill Touch's "no heal", Ray of Frost's slow, etc.).
    /// </summary>
    public class CompProperties_AbilityDamageTarget : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public float amount = 10f;
        public int hits = 1;
        public float lifestealFraction = 0f;       // heal caster by this fraction of damage dealt
        public float bonusVsWoundedFactor = 0f;    // extra damage multiplier if target already injured
        public HediffDef applyHediff;              // debuff applied to the target on hit
        public float applyHediffSeverity = 1f;

        public CompProperties_AbilityDamageTarget() => compClass = typeof(CompAbilityEffect_DamageTarget);
    }

    public class CompAbilityEffect_DamageTarget : CompAbilityEffect
    {
        public new CompProperties_AbilityDamageTarget Props => (CompProperties_AbilityDamageTarget)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var thing = target.Thing;
            if (thing == null)
            {
                return;
            }
            DealTo(thing);
            // Sorcerer Twinned Spell: a single-target spell also strikes a second nearby foe.
            if (thing is Pawn && ClassFeatures.HasMetamagic(parent.pawn, "RH_Feat_Twinned"))
            {
                var twin = FindTwin(thing);
                if (twin != null) DealTo(twin);
            }
        }

        private void DealTo(Thing thing)
        {
            float amount = Props.amount * SpellPower.For(parent.pawn);
            if (Props.bonusVsWoundedFactor > 0f && thing is Pawn wp && wp.health.summaryHealth.SummaryHealthPercent < 1f)
            {
                amount *= 1f + Props.bonusVsWoundedFactor;
            }
            for (int i = 0; i < Props.hits; i++)
            {
                thing.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Blunt, amount, 1f, -1f, parent.pawn));
            }
            if (Props.applyHediff != null && thing is Pawn hp && !hp.Dead)
            {
                var h = hp.health.AddHediff(Props.applyHediff);
                if (h != null) h.Severity = Props.applyHediffSeverity;
            }
            if (Props.lifestealFraction > 0f && parent.pawn != null && !parent.pawn.Dead)
            {
                HealPawn(parent.pawn, amount * Props.hits * Props.lifestealFraction);
            }
        }

        private Thing FindTwin(Thing primary)
        {
            var map = parent.pawn?.MapHeld;
            if (map == null) return null;
            Thing best = null;
            float bestDist = 999f;
            foreach (var p in GenRadial.RadialDistinctThingsAround(primary.Position, map, 6.9f, true).OfType<Pawn>())
            {
                if (p == primary || p.Dead || !p.HostileTo(parent.pawn)) continue;
                float d = primary.Position.DistanceTo(p.Position);
                if (d < bestDist) { best = p; bestDist = d; }
            }
            return best;
        }

        internal static void HealPawn(Pawn pawn, float total)
        {
            float remaining = total;
            foreach (var inj in pawn.health.hediffSet.hediffs.OfType<Hediff_Injury>()
                .Where(i => !i.IsPermanent()).OrderByDescending(i => i.Severity).ToList())
            {
                if (remaining <= 0f) break;
                float heal = Mathf.Min(remaining, inj.Severity);
                inj.Heal(heal);
                remaining -= heal;
            }
        }
    }

    /// <summary>
    /// Heals injuries worst-first (Cure Wounds family). With radius &gt; 0, heals every ally pawn in
    /// the radius (Mass Cure Wounds / Prayer of Healing). Scales with Spell Power.
    /// </summary>
    public class CompProperties_AbilityHeal : CompProperties_AbilityEffect
    {
        public float amount = 15f;
        public float radius = 0f;       // 0 = single target; >0 = heal allies in radius around target cell
        public bool onlyAllies = true;

        public CompProperties_AbilityHeal() => compClass = typeof(CompAbilityEffect_Heal);
    }

    public class CompAbilityEffect_Heal : CompAbilityEffect
    {
        public new CompProperties_AbilityHeal Props => (CompProperties_AbilityHeal)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            float each = Props.amount * SpellPower.For(parent.pawn);
            if (Props.radius > 0f)
            {
                var map = parent.pawn?.MapHeld;
                if (map == null) return;
                foreach (var p in GenRadial.RadialDistinctThingsAround(target.Cell, map, Props.radius, true)
                             .OfType<Pawn>().ToList())
                {
                    if (p.Dead) continue;
                    if (Props.onlyAllies && p.HostileTo(parent.pawn)) continue;
                    CompAbilityEffect_DamageTarget.HealPawn(p, each);
                }
                return;
            }
            if (target.Thing is Pawn targetPawn && !targetPawn.Dead)
            {
                CompAbilityEffect_DamageTarget.HealPawn(targetPawn, each);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (Props.radius > 0f) return base.Valid(target, throwMessages);
            return target.Thing is Pawn p && !p.Dead && base.Valid(target, throwMessages);
        }
    }

    /// <summary>
    /// Applies a hediff (buff or debuff) to every pawn in a radius - ally buffs (Bless), enemy
    /// debuffs (Bane, Faerie Fire), or a damaging aura around a point (Spirit Guardians, via a
    /// hediff that ticks damage). targetMode picks who is affected.
    /// </summary>
    public class CompProperties_AbilityZoneHediff : CompProperties_AbilityEffect
    {
        public HediffDef hediff;
        public float radius = 4.9f;
        public float severity = 1f;
        public ZoneTargets targets = ZoneTargets.Allies;

        public CompProperties_AbilityZoneHediff() => compClass = typeof(CompAbilityEffect_ZoneHediff);
    }

    public enum ZoneTargets { Allies, Enemies, All }

    public class CompAbilityEffect_ZoneHediff : CompAbilityEffect
    {
        public new CompProperties_AbilityZoneHediff Props => (CompProperties_AbilityZoneHediff)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var caster = parent.pawn;
            var map = caster?.MapHeld;
            if (Props.hediff == null || map == null) return;
            foreach (var p in GenRadial.RadialDistinctThingsAround(target.Cell, map, Props.radius, true)
                         .OfType<Pawn>().ToList())
            {
                if (p.Dead) continue;
                bool hostile = p.HostileTo(caster);
                if (Props.targets == ZoneTargets.Allies && hostile) continue;
                if (Props.targets == ZoneTargets.Enemies && !hostile) continue;
                if (Props.targets == ZoneTargets.Allies && p == caster) { /* include self */ }
                var h = p.health.AddHediff(Props.hediff);
                if (h != null) h.Severity = Props.severity;
            }
        }
    }

    /// <summary>Removes hediffs of the given defs (or a hediff tag/category) from the target
    /// (Lesser/Greater Restoration, cure poison, dispel a debuff).</summary>
    public class CompProperties_AbilityCleanse : CompProperties_AbilityEffect
    {
        public System.Collections.Generic.List<HediffDef> hediffs;
        public bool removeBadDiseases = false;   // also remove any tox/disease-type hediffs
        public int maxToRemove = 99;

        public CompProperties_AbilityCleanse() => compClass = typeof(CompAbilityEffect_Cleanse);
    }

    public class CompAbilityEffect_Cleanse : CompAbilityEffect
    {
        public new CompProperties_AbilityCleanse Props => (CompProperties_AbilityCleanse)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!(target.Thing is Pawn p) || p.Dead) return;
            int removed = 0;
            foreach (var h in p.health.hediffSet.hediffs.ToList())
            {
                if (removed >= Props.maxToRemove) break;
                bool match = (Props.hediffs != null && Props.hediffs.Contains(h.def))
                    || (Props.removeBadDiseases && h.def.makesSickThought);
                if (match) { p.health.RemoveHediff(h); removed++; }
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            return target.Thing is Pawn p && !p.Dead && base.Valid(target, throwMessages);
        }
    }

    /// <summary>Revivify: resurrect a fresh corpse, no side effects (5e: get them up quick).</summary>
    public class CompProperties_AbilityRevivify : CompProperties_AbilityEffect
    {
        public int maxDeathTicks = 60000; // one in-game day

        public CompProperties_AbilityRevivify() => compClass = typeof(CompAbilityEffect_Revivify);
    }

    public class CompAbilityEffect_Revivify : CompAbilityEffect
    {
        public new CompProperties_AbilityRevivify Props => (CompProperties_AbilityRevivify)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (!(target.Thing is Corpse corpse))
            {
                return;
            }
            var innerPawn = corpse.InnerPawn; // resurrection destroys the corpse - capture first
            if (innerPawn != null && ResurrectionUtility.TryResurrect(innerPawn))
            {
                Messages.Message("RH_Revivified".Translate(innerPawn.LabelShortCap, parent.pawn.LabelShortCap),
                    innerPawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!(target.Thing is Corpse corpse))
            {
                return false;
            }
            if (corpse.Age > Props.maxDeathTicks || corpse.GetRotStage() != RotStage.Fresh)
            {
                if (throwMessages)
                {
                    Messages.Message("RH_TooLongDead".Translate(), corpse, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }
            return base.Valid(target, throwMessages);
        }
    }

    /// <summary>Summons a familiar pawn of the caster's faction beside the caster (Warlock Pact of the Chain imp).</summary>
    public class CompProperties_AbilitySummon : CompProperties_AbilityEffect
    {
        public PawnKindDef pawnKind;

        public CompProperties_AbilitySummon() => compClass = typeof(CompAbilityEffect_Summon);
    }

    public class CompAbilityEffect_Summon : CompAbilityEffect
    {
        public new CompProperties_AbilitySummon Props => (CompProperties_AbilitySummon)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var caster = parent.pawn;
            var map = caster?.MapHeld;
            if (map == null || Props.pawnKind == null) return;

            // One familiar at a time: dismiss any previous one of this kind in the caster's faction.
            foreach (var old in map.mapPawns.SpawnedPawnsInFaction(caster.Faction)
                         .Where(p => p.kindDef == Props.pawnKind).ToList())
            {
                old.Destroy();
            }

            var familiar = PawnGenerator.GeneratePawn(new PawnGenerationRequest(Props.pawnKind, caster.Faction));
            var cell = CellFinder.RandomClosewalkCellNear(caster.Position, map, 3);
            GenSpawn.Spawn(familiar, cell, map);
            if (familiar.connections == null) familiar.connections = new Pawn_ConnectionsTracker(familiar);
            familiar.connections.ConnectTo(caster);
            // Bind the master so the familiar's think tree can follow and defend them.
            var devotion = familiar.health.AddHediff(RH_DefOf.RH_MimDevotion) as Hediff_MimDevotion;
            if (devotion != null) devotion.master = caster;
            Messages.Message(caster.LabelShortCap + " summons a fiendish familiar.",
                familiar, MessageTypeDefOf.PositiveEvent, historical: false);
        }
    }

    /// <summary>Turn Undead: a holy rebuke that fully fears the undead and lightly shakes everyone else.</summary>
    public class CompProperties_AbilityTurnUndead : CompProperties_AbilityEffect
    {
        public HediffDef undeadHediff;     // strong fear, for the undead
        public HediffDef livingHediff;     // weak fear, for everything else
        public float radius = 5.9f;

        public CompProperties_AbilityTurnUndead() => compClass = typeof(CompAbilityEffect_TurnUndead);
    }

    public class CompAbilityEffect_TurnUndead : CompAbilityEffect
    {
        public new CompProperties_AbilityTurnUndead Props => (CompProperties_AbilityTurnUndead)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var caster = parent.pawn;
            var map = caster?.MapHeld;
            if (map == null) return;
            foreach (var p in GenRadial.RadialDistinctThingsAround(target.Cell, map, Props.radius, true)
                         .OfType<Pawn>().ToList())
            {
                if (p.Dead || !p.HostileTo(caster)) continue;
                var def = IsUndead(p) ? Props.undeadHediff : Props.livingHediff;
                if (def != null && !p.health.hediffSet.HasHediff(def)) p.health.AddHediff(def);
            }
        }

        private static bool IsUndead(Pawn p)
        {
            if (p.IsMutant) return true; // shamblers, ghouls, and other Anomaly undead
            return p.RaceProps != null && p.RaceProps.IsAnomalyEntity;
        }
    }

    /// <summary>Centered explosion (Fireball).</summary>
    public class CompProperties_AbilityExplosion : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public float radius = 3.5f;
        public int damageAmount = -1;

        public CompProperties_AbilityExplosion() => compClass = typeof(CompAbilityEffect_Explosion);
    }

    public class CompAbilityEffect_Explosion : CompAbilityEffect
    {
        public new CompProperties_AbilityExplosion Props => (CompProperties_AbilityExplosion)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            int dmg = Props.damageAmount >= 0
                ? Mathf.RoundToInt(Props.damageAmount * SpellPower.For(parent.pawn))
                : Props.damageAmount; // -1 = use the damage def's default
            GenExplosion.DoExplosion(target.Cell, parent.pawn.MapHeld, Props.radius,
                Props.damageDef ?? DamageDefOf.Flame, parent.pawn, dmg);
        }
    }
}
