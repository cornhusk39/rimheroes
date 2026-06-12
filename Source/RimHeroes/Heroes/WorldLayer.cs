using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Enemy Heroes: large hostile raids rarely include a leveled Hero of a random class.
    /// They are the loot source for inlays and class tomes.
    /// </summary>
    [HarmonyPatch(typeof(PawnGroupMakerUtility), nameof(PawnGroupMakerUtility.GeneratePawns))]
    public static class Patch_PawnGroupMakerUtility_GeneratePawns
    {
        public const float PointsThreshold = 2000f;
        public const float HeroChance = 0.2f;

        public static void Postfix(ref IEnumerable<Pawn> __result, PawnGroupMakerParms parms)
        {
            if (!RimHeroesMod.Settings.enemyHeroRaids
                || parms.groupKind != PawnGroupKindDefOf.Combat
                || parms.points < PointsThreshold
                || parms.faction == null
                || Faction.OfPlayerSilentFail == null
                || !parms.faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }
            var pawns = __result.ToList();
            __result = pawns;
            if (!Rand.Chance(HeroChance))
            {
                return;
            }
            var candidate = pawns.FirstOrDefault(p => p.RaceProps.Humanlike && p.story != null && !HeroUtility.IsHero(p));
            if (candidate == null)
            {
                return;
            }
            var classDef = DefDatabase<HeroClassDef>.AllDefsListForReading.RandomElement();
            var hediff = HeroUtility.MakeHero(candidate, classDef);
            if (hediff != null)
            {
                hediff.SetLevelDirect(Mathf.Clamp(Mathf.RoundToInt(parms.points / 600f), 3, 13));
                Log.Message($"[RimHeroes] Enemy hero joins the raid: {candidate.LabelShort}, {classDef.label} {hediff.level} ({parms.points:F0} pts)");
            }
        }
    }

    /// <summary>Enemy Heroes drop the goods: an inlay (lesser-weighted) and sometimes their class tome.</summary>
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    public static class Patch_Pawn_Kill_HeroLoot
    {
        public static void Postfix(Pawn __instance)
        {
            var hediff = HeroUtility.GetHeroHediff(__instance);
            if (hediff == null || __instance.Faction == Faction.OfPlayerSilentFail)
            {
                return;
            }
            var map = __instance.Corpse?.MapHeld ?? __instance.MapHeld;
            var pos = __instance.Corpse?.PositionHeld ?? __instance.PositionHeld;
            if (map == null || !pos.IsValid)
            {
                return;
            }
            string tier = Rand.Value < 0.55f ? "Lesser" : Rand.Value < 0.8f ? "Regular" : "Greater";
            string family = new[] { "Warding", "Keenness", "Fleetness" }.RandomElement();
            var inlay = DefDatabase<ThingDef>.GetNamedSilentFail($"RH_InlayItem_{family}_{tier}");
            if (inlay != null)
            {
                GenSpawn.Spawn(ThingMaker.MakeThing(inlay), pos, map);
            }
            if (Rand.Chance(0.12f) && hediff.classDef != null)
            {
                var tome = DefDatabase<ThingDef>.GetNamedSilentFail($"RH_Tome_{hediff.classDef.defName.Replace("RH_", "")}");
                if (tome != null)
                {
                    GenSpawn.Spawn(ThingMaker.MakeThing(tome), pos, map);
                }
            }
        }
    }

    /// <summary>
    /// Hero corpses cannot be cremated or butchered (revival needs a body) unless the player
    /// opts out in settings. One hook covers crematoria and butcher tables alike.
    /// </summary>
    [HarmonyPatch(typeof(Bill), nameof(Bill.IsFixedOrAllowedIngredient), typeof(Thing))]
    public static class Patch_Bill_IsFixedOrAllowedIngredient
    {
        public static void Postfix(Bill __instance, Thing thing, ref bool __result)
        {
            if (__result && HeroUtility.CorpseBillBlocked(__instance.recipe, thing))
            {
                __result = false;
            }
        }
    }

    public class Alert_HeroCorpseUnburied : Alert
    {
        private readonly List<Thing> culprits = new List<Thing>();

        public Alert_HeroCorpseUnburied()
        {
            defaultLabel = "RH_AlertHeroUnburied".Translate();
            defaultExplanation = "RH_AlertHeroUnburiedDesc".Translate();
            defaultPriority = AlertPriority.High;
        }

        public override AlertReport GetReport()
        {
            culprits.Clear();
            foreach (var map in Find.Maps)
            {
                if (!map.IsPlayerHome)
                {
                    continue;
                }
                foreach (var thing in map.listerThings.ThingsInGroup(ThingRequestGroup.Corpse))
                {
                    if (thing is Corpse corpse && corpse.Spawned
                        && corpse.InnerPawn?.Faction == Faction.OfPlayerSilentFail
                        && HeroUtility.IsHero(corpse.InnerPawn))
                    {
                        culprits.Add(corpse);
                    }
                }
            }
            return AlertReport.CulpritsAre(culprits);
        }
    }
}
