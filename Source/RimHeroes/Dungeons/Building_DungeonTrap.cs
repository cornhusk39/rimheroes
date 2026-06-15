using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    /// <summary>
    /// A hidden crypt trap, resolved spot-then-save. While hidden it draws nothing; a party member who
    /// comes close enough rolls a silent Perception check (sight + intellect) and, on a success, reveals
    /// it so the party can step around it or disarm it. A revealed trap can be disarmed by hand (a
    /// Perception roll; fumble it and it goes off in your hands). If anyone in the party steps onto it
    /// while it is still armed, that pawn rolls a Dodge save (the Moving stat) to throw themselves clear;
    /// fail and the trap's blades or darts catch them. Either way the trap is a one-shot and spends itself.
    /// Only the player's party trips traps, so the crypt's own undead don't disarm them for you.
    /// </summary>
    public class Building_DungeonTrap : Building
    {
        private bool hidden = true;
        private bool triggered;
        private HashSet<Pawn> spotAttempted = new HashSet<Pawn>();

        public bool IsHidden => hidden;

        private TrapExtension Ext => def.GetModExtension<TrapExtension>() ?? new TrapExtension();
        private static CheckDef SaveCheck => DefDatabase<CheckDef>.GetNamedSilentFail("RH_Check_TrapSave");
        private static CheckDef SpotCheck => DefDatabase<CheckDef>.GetNamedSilentFail("RH_Check_TrapSpot");
        private static CheckDef DisarmCheck => DefDatabase<CheckDef>.GetNamedSilentFail("RH_Check_TrapDisarm");

        private static bool IsParty(Pawn p) =>
            p != null && !p.Dead && !p.Downed && p.RaceProps.Humanlike && p.Faction != null && p.Faction.IsPlayer;

        public override void Tick()
        {
            base.Tick();
            if (triggered) return;
            var map = Map;
            if (map == null) return;

            // spring: a party member standing on the trap
            var here = Position.GetThingList(map);
            for (int i = 0; i < here.Count; i++)
            {
                if (here[i] is Pawn pawn && pawn.Position == Position && IsParty(pawn)) { Trigger(pawn); return; }
            }

            // passive spot on a relaxed cadence: each nearby party member gets one quiet look
            if (hidden && this.IsHashIntervalTick(30))
            {
                foreach (var p in map.mapPawns.FreeColonistsSpawned)
                {
                    if (!spotAttempted.Contains(p) && p.Position.InHorDistOf(Position, Ext.spotRadius))
                        TrySpot(p);
                }
            }
        }

        /// <summary>One silent Perception look. Reveals the trap on a pass. forcedRaw is for the dev spike.</summary>
        public bool TrySpot(Pawn p, int forcedRaw = 0)
        {
            if (!hidden) return false;
            spotAttempted.Add(p);
            var result = D20.Roll(CheckMods.GetModifier(p, SpotCheck), Ext.spotDC, RollAdvantage.None, forcedRaw);
            if (!result.Passed) return false;
            hidden = false;
            Messages.Message("RH_TrapSpotted".Translate(p.LabelShortCap), new LookTargets(this), MessageTypeDefOf.NeutralEvent, false);
            return true;
        }

        private void Trigger(Pawn victim)
        {
            if (triggered) return;
            triggered = true;
            hidden = false;   // springing reveals it
            if (victim.IsColonistPlayerControlled)
            {
                int mod = CheckMods.GetModifier(victim, SaveCheck);
                var result = D20.Roll(mod, Ext.saveDC);
                Find.WindowStack.Add(new Dialog_D20Roll(victim, SaveCheck, result, r => ResolveSave(victim, r)));
            }
            else
            {
                ResolveSave(victim, D20.Roll(CheckMods.GetModifier(victim, SaveCheck), Ext.saveDC));
            }
        }

        /// <summary>Apply a resolved Dodge save. Pass = clear; fail = the trap catches the pawn. One-shot.</summary>
        public void ResolveSave(Pawn victim, RollResult result)
        {
            if (result.Passed)
                Messages.Message("RH_TrapDodged".Translate(victim.LabelShortCap), new LookTargets(victim), MessageTypeDefOf.PositiveEvent, false);
            else
                Hit(victim, result.bucket == RollBucket.CriticalFailure);
            Consume(false);
        }

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            if (hidden || triggered) yield break;
            yield return new Command_Action
            {
                defaultLabel = "RH_TrapDisarm".Translate(),
                defaultDesc = "RH_TrapDisarmDesc".Translate(Ext.disarmDC),
                action = OfferDisarmers
            };
        }

        private void OfferDisarmers()
        {
            var check = DisarmCheck;
            var options = new List<FloatMenuOption>();
            foreach (var p in Map.mapPawns.FreeColonistsSpawned
                         .Where(p => !p.Downed && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false)
                                     && p.CanReach(this, PathEndMode.Touch, Danger.Deadly))
                         .OrderByDescending(p => CheckMods.GetModifier(p, check)))
            {
                Pawn disarmer = p;
                int mod = CheckMods.GetModifier(disarmer, check);
                options.Add(new FloatMenuOption("RH_TrapDisarmerOption".Translate(disarmer.LabelShortCap, Signed(mod)),
                    () => BeginDisarm(disarmer)));
            }
            if (options.Count == 0) options.Add(new FloatMenuOption("RH_TrapNoDisarmer".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void BeginDisarm(Pawn p)
        {
            if (triggered) return;
            triggered = true;   // lock out the step-on spring while the attempt resolves
            int mod = CheckMods.GetModifier(p, DisarmCheck);
            var result = D20.Roll(mod, Ext.disarmDC);
            Find.WindowStack.Add(new Dialog_D20Roll(p, DisarmCheck, result, r => ResolveDisarm(p, r)));
        }

        /// <summary>Apply a resolved disarm. Pass = neutralized safely; fail = it springs on the disarmer.</summary>
        public void ResolveDisarm(Pawn p, RollResult result)
        {
            if (result.Passed)
            {
                Consume(true);
            }
            else
            {
                hidden = false;
                Hit(p, result.bucket == RollBucket.CriticalFailure);
                Consume(false);
            }
        }

        private void Hit(Pawn victim, bool critical)
        {
            var ext = Ext;
            var dmgDef = ext.damage ?? DamageDefOf.Cut;
            int amt = ext.damageAmount.RandomInRange;
            if (critical) amt = Mathf.RoundToInt(amt * 1.5f);
            victim.TakeDamage(new DamageInfo(dmgDef, amt, 0f, -1f, this));
            Messages.Message("RH_TrapHit".Translate(victim.LabelShortCap), new LookTargets(victim), MessageTypeDefOf.ThreatSmall, false);
        }

        private void Consume(bool disarmedSafely)
        {
            if (disarmedSafely)
                Messages.Message("RH_TrapDisarmed".Translate(), new LookTargets(Position, Map), MessageTypeDefOf.PositiveEvent, false);
            if (!Destroyed) Destroy(DestroyMode.Vanish);
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (hidden) return;   // unrevealed traps are invisible
            Graphic.Draw(drawLoc, flip ? Rotation.Opposite : Rotation, this);
        }

        public override string GetInspectString()
        {
            if (hidden) return base.GetInspectString();
            var sb = new System.Text.StringBuilder(base.GetInspectString());
            if (sb.Length > 0) sb.AppendLine();
            sb.Append("RH_TrapStateArmed".Translate());
            return sb.ToString();
        }

        private static string Signed(int n) => (n >= 0 ? "+" : "") + n;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref hidden, "hidden", true);
            Scribe_Values.Look(ref triggered, "triggered");
            if (Scribe.mode == LoadSaveMode.PostLoadInit && spotAttempted == null)
                spotAttempted = new HashSet<Pawn>();
        }
    }
}
