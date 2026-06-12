using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    public enum InlaySlot
    {
        Defense,
        Offense,
        Utility
    }

    public enum InlayTier
    {
        Lesser = 1,
        Regular = 2,
        Greater = 3
    }

    /// <summary>Marks a hediff as a vestment inlay and declares which slot it occupies.</summary>
    public class InlayExtension : DefModExtension
    {
        public InlaySlot slot;
        public InlayTier tier = InlayTier.Regular;
    }

    public static class InlayUtility
    {
        public static InlayExtension ExtensionOf(HediffDef def) => def?.GetModExtension<InlayExtension>();

        public static IEnumerable<Hediff> InstalledInlays(Pawn pawn) =>
            pawn.health.hediffSet.hediffs.Where(h => ExtensionOf(h.def) != null);

        public static Hediff InlayInSlot(Pawn pawn, InlaySlot slot) =>
            InstalledInlays(pawn).FirstOrDefault(h => ExtensionOf(h.def).slot == slot);

        /// <summary>
        /// Installs an inlay, replacing whatever occupies its slot (the old inlay is destroyed -
        /// it was worked into the armor). One inlay per slot; three slots per vestment.
        /// </summary>
        public static Hediff Install(Pawn pawn, HediffDef inlayDef)
        {
            var ext = ExtensionOf(inlayDef);
            if (ext == null)
            {
                Log.Error($"[RimHeroes] {inlayDef?.defName} installed as inlay but has no InlayExtension");
                return null;
            }
            var occupant = InlayInSlot(pawn, ext.slot);
            if (occupant != null && occupant.def != inlayDef)
            {
                pawn.health.RemoveHediff(occupant);
                if (PawnUtility.ShouldSendNotificationAbout(pawn))
                {
                    Messages.Message("RH_InlayReplaced".Translate(pawn.LabelShortCap, occupant.def.label, inlayDef.label),
                        pawn, MessageTypeDefOf.NeutralEvent);
                }
            }
            return pawn.health.AddHediff(inlayDef);
        }
    }
}
