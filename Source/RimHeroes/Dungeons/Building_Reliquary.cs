using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    /// <summary>
    /// The boss-vault reliquary: a warded coffer that holds the crypt's rare inlay drop behind a d20
    /// lock. Select it, hit "Pick the lock", and choose who attempts (the menu lists reachable
    /// colonists best-first by their lockpick modifier, so you can send your specialist). The lock
    /// starts at DC 5/10/15 by tier and climbs +5 with every failed attempt; once it would reach 25 it
    /// jams for good. A natural 20 always pops it open. Success spills the inlays it was guarding.
    /// </summary>
    public class Building_Reliquary : Building
    {
        public int lockTier = 3;        // 1..3 -> starting DC 5 / 10 / 15
        private int failures;           // failed attempts so far; current DC = StartDC + 5 * failures
        private bool jammed;            // permanently sealed (DC would have reached 25)
        private bool opened;
        private bool stocked;
        private List<ThingDef> contents = new List<ThingDef>();

        private const int SealDC = 25;
        public const string KeyDef = "RH_ReliquaryKey";
        public const string LockpickDef = "RH_Lockpick";
        public const int LockpickDcReduction = 7;

        private int StartDC => lockTier <= 1 ? 5 : (lockTier == 2 ? 10 : 15);
        public int CurrentDC => StartDC + 5 * failures;
        public bool CanAttempt => !opened && !jammed && CurrentDC < SealDC;

        public static int ReducedDC(int dc) => Mathf.Max(1, dc - LockpickDcReduction);

        private static CheckDef Check => DefDatabase<CheckDef>.GetNamedSilentFail("RH_Check_Reliquary");

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad && !stocked) Stock();
        }

        /// <summary>Decide the loot the reliquary guards: one guaranteed Greater inlay (the rare draw),
        /// and a coin-flip for an extra Regular alongside it.</summary>
        public void Stock()
        {
            stocked = true;
            var greater = InlaysWith("_Greater");
            if (greater.Count > 0) contents.Add(greater.RandomElement());
            if (Rand.Chance(0.5f))
            {
                var regular = InlaysWith("_Regular");
                if (regular.Count > 0) contents.Add(regular.RandomElement());
            }
        }

        private static List<ThingDef> InlaysWith(string suffix) =>
            DefDatabase<ThingDef>.AllDefs
                .Where(d => d.defName.StartsWith("RH_InlayItem_") && d.defName.EndsWith(suffix))
                .ToList();

        public override IEnumerable<Gizmo> GetGizmos()
        {
            foreach (var g in base.GetGizmos()) yield return g;
            if (!CanAttempt) yield break;

            var cmd = new Command_Action
            {
                defaultLabel = "RH_ReliquaryPick".Translate(),
                defaultDesc = "RH_ReliquaryPickDesc".Translate(CurrentDC),
                action = OfferPickers
            };
            yield return cmd;
        }

        private void OfferPickers()
        {
            var check = Check;
            var options = new List<FloatMenuOption>();
            foreach (var p in Map.mapPawns.FreeColonistsSpawned
                         .Where(p => !p.Downed && (p.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) ?? false)
                                     && p.CanReach(this, PathEndMode.Touch, Danger.Deadly))
                         .OrderByDescending(p => CheckMods.GetModifier(p, check)))
            {
                Pawn picker = p;
                int mod = CheckMods.GetModifier(picker, check);

                // a held reliquary key skips the roll entirely
                if (CountItem(picker, KeyDef) > 0)
                    options.Add(new FloatMenuOption("RH_ReliquaryUseKey".Translate(picker.LabelShortCap), () => TryOpenWithKey(picker)));

                // a held lockpick cuts the DC by 7 and is spent on the attempt
                if (CountItem(picker, LockpickDef) > 0)
                    options.Add(new FloatMenuOption("RH_ReliquaryUseLockpick".Translate(picker.LabelShortCap, ReducedDC(CurrentDC)), () => BeginAttempt(picker, true)));

                string label = "RH_ReliquaryPickerOption".Translate(picker.LabelShortCap, Signed(mod));
                options.Add(new FloatMenuOption(label, () => BeginAttempt(picker, false)));
            }

            if (options.Count == 0)
                options.Add(new FloatMenuOption("RH_ReliquaryNoPicker".Translate(), null));
            Find.WindowStack.Add(new FloatMenu(options));
        }

        private void BeginAttempt(Pawn picker, bool useLockpick)
        {
            if (!CanAttempt) return;
            int dc = CurrentDC;
            if (useLockpick)
            {
                if (ConsumeOne(picker, LockpickDef)) dc = ReducedDC(CurrentDC);
                else useLockpick = false;   // none left after all; roll the lock straight
            }
            var check = Check;
            int mod = CheckMods.GetModifier(picker, check);
            RollResult result = D20.Roll(mod, dc);   // DC drives both display and pass/fail
            Find.WindowStack.Add(new Dialog_D20Roll(picker, check, result, r => ResolveAttempt(picker, r)));
        }

        /// <summary>Spend a reliquary key the picker is carrying to open the lock outright. Returns false
        /// if the lock can't be attempted or the pawn has no key.</summary>
        public bool TryOpenWithKey(Pawn picker)
        {
            if (!CanAttempt || !ConsumeOne(picker, KeyDef)) return false;
            Messages.Message("RH_ReliquaryKeyUsed".Translate(picker.LabelShortCap), new LookTargets(this), MessageTypeDefOf.PositiveEvent, false);
            OpenAndSpill(picker);
            return true;
        }

        public static int CountItem(Pawn p, string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || p?.inventory == null) return 0;
            return p.inventory.innerContainer.Where(t => t.def == def).Sum(t => t.stackCount);
        }

        /// <summary>Remove one of the named item from the pawn's carried inventory. Returns false if none.</summary>
        public static bool ConsumeOne(Pawn p, string defName)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (def == null || p?.inventory == null) return false;
            var thing = p.inventory.innerContainer.FirstOrDefault(t => t.def == def);
            if (thing == null) return false;
            if (thing.stackCount > 1) thing.stackCount--;
            else { p.inventory.innerContainer.Remove(thing); thing.Destroy(); }
            return true;
        }

        /// <summary>Apply a resolved roll. Split out from the dialog so the dev spike can drive it with
        /// forced rolls. A passed roll opens the reliquary; a failure raises the DC, and if that would
        /// hit the seal threshold the lock jams permanently.</summary>
        public void ResolveAttempt(Pawn picker, RollResult result)
        {
            if (!CanAttempt) return;
            if (result.Passed)
            {
                OpenAndSpill(picker);
                return;
            }
            failures++;
            if (CurrentDC >= SealDC)
            {
                jammed = true;
                Messages.Message("RH_ReliquaryJammed".Translate(), new LookTargets(this), MessageTypeDefOf.NegativeEvent, false);
            }
            else
            {
                Messages.Message("RH_ReliquaryFailed".Translate(CurrentDC), new LookTargets(this), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void OpenAndSpill(Pawn picker)
        {
            opened = true;
            var map = Map;
            var pos = Position;
            foreach (var def in contents)
            {
                var t = ThingMaker.MakeThing(def);
                t.stackCount = 1;
                GenPlace.TryPlaceThing(t, pos, map, ThingPlaceMode.Near);
            }
            contents.Clear();
            Messages.Message("RH_ReliquaryOpened".Translate(picker.LabelShortCap), new LookTargets(pos, map), MessageTypeDefOf.PositiveEvent, false);
        }

        public override string GetInspectString()
        {
            var sb = new System.Text.StringBuilder(base.GetInspectString());
            if (sb.Length > 0) sb.AppendLine();
            if (opened) sb.Append("RH_ReliquaryStateOpened".Translate());
            else if (jammed) sb.Append("RH_ReliquaryStateJammed".Translate());
            else sb.Append("RH_ReliquaryStateLocked".Translate(CurrentDC));
            return sb.ToString();
        }

        private static string Signed(int n) => (n >= 0 ? "+" : "") + n;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref lockTier, "lockTier", 3);
            Scribe_Values.Look(ref failures, "failures");
            Scribe_Values.Look(ref jammed, "jammed");
            Scribe_Values.Look(ref opened, "opened");
            Scribe_Values.Look(ref stocked, "stocked");
            Scribe_Collections.Look(ref contents, "contents", LookMode.Def);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && contents == null)
                contents = new List<ThingDef>();
        }
    }
}
