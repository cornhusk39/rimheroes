using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A hurled fireball that detonates on impact, its blast scaled by the caster's Spell Power. Reads
    /// damageAmountBase (base blast damage) and explosionRadius off the projectile def. Used by the
    /// Fireball spell so it now flies a visible ball of flame to the target before exploding (the spell
    /// previously detonated instantly with no projectile).
    /// </summary>
    public class Projectile_RHFireball : Projectile
    {
        public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            IntVec3 cell = Position;
            base.Impact(hitThing, blockedByShield);
            if (map == null) return;

            float mult = launcher is Pawn p ? SpellPower.For(p) : 1f;
            int baseDmg = def.projectile.damageAmountBase > 0 ? def.projectile.damageAmountBase : 35;
            int dmg = Mathf.RoundToInt(baseDmg * mult);
            GenExplosion.DoExplosion(cell, map, def.projectile.explosionRadius,
                def.projectile.damageDef ?? DamageDefOf.Flame, launcher, dmg);
        }
    }
}
