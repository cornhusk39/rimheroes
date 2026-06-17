using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A single-target spell projectile whose impact damage scales with the caster's Spell Power.
    /// Reads damageAmountBase / damageDef / armorPenetrationBase off the projectile def. Used by the
    /// orb/dart spells (Magic Missile, Chromatic Orb, Produce Flame) so they fly a visible bolt and
    /// still scale like the rest of the kit.
    /// </summary>
    public class Projectile_RHSpellDart : Projectile
    {
        public override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            // Spell attack roll: a tagged bolt (Fire Bolt, Chromatic Orb, Produce Flame) can miss its mark.
            if (hitThing is Pawn victim && !victim.Dead && SpellAccuracy.UsesAttackRoll(def)
                && !SpellAccuracy.Hits(launcher as Pawn, victim))
            {
                SpellAccuracy.ThrowMiss(victim);
                base.Impact(null, blockedByShield);
                return;
            }
            if (hitThing != null)
            {
                float mult = launcher is Pawn p ? SpellPower.For(p) : 1f;
                int baseDmg = def.projectile.damageAmountBase > 0 ? def.projectile.damageAmountBase : 6;
                var dinfo = new DamageInfo(def.projectile.damageDef ?? DamageDefOf.Blunt,
                    GenMath.RoundRandom(baseDmg * mult), def.projectile.armorPenetrationBase, -1f, launcher);
                hitThing.TakeDamage(dinfo);
            }
            // null hitThing so the base impact does cleanup + impact fleck without re-damaging the target.
            base.Impact(null, blockedByShield);
        }
    }

    /// <summary>
    /// Chromatic Orb: same scaled impact as the dart, but it spins as it flies and sheds quick
    /// elemental sparks in every direction along the way.
    /// </summary>
    public class Projectile_RHChromaticOrb : Projectile_RHSpellDart
    {
        private static readonly Color[] Elemental =
        {
            new Color(1f, 0.35f, 0.23f),  // fire
            new Color(1f, 0.82f, 0.29f),  // lightning
            new Color(0.31f, 0.82f, 0.42f), // acid
            new Color(0.48f, 0.42f, 1f),  // force
            new Color(1f, 0.36f, 0.80f)   // radiant pink
        };

        private int spin;

        public override void Tick()
        {
            base.Tick();
            spin++;
            if (!Spawned || Map == null || spin % 2 != 0) return;
            Vector3 c = DrawPos;
            for (int i = 0; i < 2; i++)
            {
                Vector3 l = c + new Vector3(Rand.Range(-0.15f, 0.15f), 0f, Rand.Range(-0.15f, 0.15f));
                if (!l.ShouldSpawnMotesAt(Map)) continue;
                var d = FleckMaker.GetDataStatic(l, Map, RH_FleckDefOf.RH_Mote_Spark, Rand.Range(0.35f, 0.55f));
                Color col = Elemental[Rand.Range(0, Elemental.Length)]; col.a = 0.8f;
                d.instanceColor = col;
                d.velocityAngle = Rand.Range(0f, 360f);   // fling outward in every direction
                d.velocitySpeed = Rand.Range(0.7f, 1.5f);
                Map.flecks.CreateFleck(d);
            }
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Graphic.Draw(drawLoc, Rot4.North, this, spin * 9f); // visible glassy spin
        }
    }

    /// <summary>Fires a short staggered volley of projectiles at one target (Magic Missile's 3 darts).</summary>
    public class CompProperties_AbilityLaunchVolley : CompProperties_AbilityEffect
    {
        public ThingDef projectileDef;
        public int count = 3;
        public int staggerTicks = 6;

        public CompProperties_AbilityLaunchVolley() => compClass = typeof(CompAbilityEffect_LaunchVolley);
    }

    public class CompAbilityEffect_LaunchVolley : CompAbilityEffect
    {
        public new CompProperties_AbilityLaunchVolley Props => (CompProperties_AbilityLaunchVolley)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            var caster = parent.pawn;
            var map = caster?.MapHeld;
            if (map == null || Props.projectileDef == null) return;
            var queue = Current.Game?.GetComponent<GameComponent_SpellVolley>();
            if (queue == null) return;
            int now = Find.TickManager.TicksGame;
            for (int i = 0; i < Props.count; i++)
            {
                queue.Enqueue(caster, map, target, Props.projectileDef, now + i * Props.staggerTicks);
            }
        }
    }

    /// <summary>Processes pending staggered spell-projectile launches (see CompAbilityEffect_LaunchVolley).</summary>
    public class GameComponent_SpellVolley : GameComponent
    {
        private struct Shot
        {
            public Pawn launcher;
            public Map map;
            public LocalTargetInfo target;
            public ThingDef proj;
            public int fireTick;
        }

        private readonly List<Shot> pending = new List<Shot>();

        public GameComponent_SpellVolley(Game game) { }

        public void Enqueue(Pawn launcher, Map map, LocalTargetInfo target, ThingDef proj, int fireTick)
        {
            pending.Add(new Shot { launcher = launcher, map = map, target = target, proj = proj, fireTick = fireTick });
        }

        public override void GameComponentTick()
        {
            if (pending.Count == 0) return;
            int now = Find.TickManager.TicksGame;
            for (int i = pending.Count - 1; i >= 0; i--)
            {
                if (now < pending[i].fireTick) continue;
                var s = pending[i];
                pending.RemoveAt(i);
                Fire(s);
            }
        }

        private static void Fire(Shot s)
        {
            if (s.launcher == null || !s.launcher.Spawned || s.map == null || s.map != s.launcher.Map) return;
            if (!s.target.IsValid) return;
            Vector3 origin = s.launcher.DrawPos + new Vector3(Rand.Range(-0.25f, 0.25f), 0f, Rand.Range(-0.1f, 0.3f));
            var proj = (Projectile)GenSpawn.Spawn(s.proj, s.launcher.Position, s.map);
            proj.Launch(s.launcher, origin, s.target, s.target, ProjectileHitFlags.IntendedTarget);
        }
    }
}
