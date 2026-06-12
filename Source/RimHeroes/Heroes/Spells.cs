using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    public enum CasterProgression
    {
        None,
        Full
        // Half (paladin/ranger), Pact (warlock) later
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

        public static int MaxSlots(CasterProgression progression, int classLevel, int spellLevel)
        {
            if (progression != CasterProgression.Full || spellLevel < 1 || spellLevel > 9)
            {
                return 0;
            }
            return FullCaster[Mathf.Clamp(classLevel, 1, 20) - 1][spellLevel - 1];
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

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason))
            {
                return true;
            }
            if (SpellLevel > 0)
            {
                var hero = Hero;
                if (hero == null)
                {
                    reason = "RH_NotAHero".Translate();
                    return true;
                }
                if (hero.RemainingSlots(SpellLevel) <= 0)
                {
                    reason = "RH_NoSlots".Translate(SpellLevel);
                    return true;
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
            return Hero?.TryExpendSlot(SpellLevel) == true;
        }
    }

    /// <summary>Auto-hit direct damage (Magic Missile-style).</summary>
    public class CompProperties_AbilityDamageTarget : CompProperties_AbilityEffect
    {
        public DamageDef damageDef;
        public float amount = 10f;
        public int hits = 1;

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
            for (int i = 0; i < Props.hits; i++)
            {
                thing.TakeDamage(new DamageInfo(Props.damageDef ?? DamageDefOf.Blunt, Props.amount, 1f, -1f, parent.pawn));
            }
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
            GenExplosion.DoExplosion(target.Cell, parent.pawn.MapHeld, Props.radius,
                Props.damageDef ?? DamageDefOf.Flame, parent.pawn, Props.damageAmount);
        }
    }
}
