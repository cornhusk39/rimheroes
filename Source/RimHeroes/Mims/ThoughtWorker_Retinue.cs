using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A hero's mood reflects their retinue: content when every mim is at their side, and a little
    /// uneasy when a companion has fallen and not yet walked back in. Drives the RH_Retinue thought.
    /// (A first pass at the "hero housing" mood; bed-placement rules are a separate future feature.)
    /// </summary>
    public class ThoughtWorker_Retinue : ThoughtWorker
    {
        public override ThoughtState CurrentStateInternal(Pawn p)
        {
            var hero = HeroUtility.GetHeroHediff(p);
            var bonds = hero?.MimBonds;
            if (bonds == null || bonds.Count == 0) return ThoughtState.Inactive;
            bool anyMissing = bonds.Any(b =>
                b.mim == null || b.mim.Dead || b.mim.Destroyed || !b.mim.SpawnedOrAnyParentSpawned);
            return ThoughtState.ActiveAtStage(anyMissing ? 1 : 0);
        }
    }
}
