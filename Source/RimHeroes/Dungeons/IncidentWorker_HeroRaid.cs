using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI.Group;

namespace RimHeroes
{
    /// <summary>
    /// An "evil adventuring party" raid: instead of a faction's stock raiders, this assembles a band of
    /// enemy HEROES of our own classes (e.g. 2 rogues, a ranger, a warlock), their number and level
    /// scaled to the threat points, and sends them to assault the colony. Used by the capstone quest.
    /// </summary>
    public class IncidentWorker_HeroRaid : IncidentWorker
    {
        public override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map)) return false;
            float points = parms.points > 0f ? parms.points : StorytellerUtility.DefaultThreatPointsNow(map);
            var faction = parms.faction
                          ?? Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false)
                          ?? Faction.OfEntities;

            int count = Mathf.Clamp(Mathf.RoundToInt(points / 450f), 2, 8);
            int level = Mathf.Clamp(Mathf.RoundToInt(points / 400f), 3, 18);
            var classes = DefDatabase<HeroClassDef>.AllDefs.ToList();
            if (classes.Count == 0) return false;

            if (!RCellFinder.TryFindRandomPawnEntryCell(out var entry, map, CellFinder.EdgeRoadChance_Hostile))
                entry = CellFinder.RandomEdgeCell(map);

            var party = new List<Pawn>();
            var byClass = new Dictionary<HeroClassDef, int>();
            for (int i = 0; i < count; i++)
            {
                var cls = classes.RandomElement();
                var hero = MakeEnemyHero(map, faction, cls, level);
                if (hero == null) continue;
                if (!CellFinder.TryFindRandomCellNear(entry, map, 6, c => c.Standable(map) && c.GetEdifice(map) == null, out var cell)) cell = entry;
                GenSpawn.Spawn(hero, cell, map);
                party.Add(hero);
                byClass[cls] = byClass.TryGetValue(cls, out var n) ? n + 1 : 1;
            }
            if (party.Count == 0) return false;

            try { LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, party); }
            catch (System.Exception e) { Log.Warning($"[RimHeroes] hero-raid lord failed: {e.Message}"); }

            SendStandardLetter("RH_HeroRaidLabel".Translate(), "RH_HeroRaidText".Translate(Composition(byClass)),
                LetterDefOf.ThreatBig, parms, party);
            return true;
        }

        private static Pawn MakeEnemyHero(Map map, Faction faction, HeroClassDef cls, int level)
        {
            try
            {
                var hero = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, faction, forceGenerateNewPawn: true));
                var levels = HeroUtility.MakeHero(hero, cls);
                levels?.GainXP(level * 150f);
                int tier = Mathf.Clamp(Mathf.CeilToInt(level / 4f), 1, 5);
                var wpn = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Weapon_" + cls.defName.Substring(3) + "_T" + tier);
                if (wpn != null && hero.equipment != null)
                {
                    var w = (ThingWithComps)ThingMaker.MakeThing(wpn);
                    hero.equipment.MakeRoomFor(w);
                    hero.equipment.AddEquipment(w);
                }
                return hero;
            }
            catch (System.Exception e) { Log.Warning($"[RimHeroes] enemy hero gen failed: {e.Message}"); return null; }
        }

        private static string Composition(Dictionary<HeroClassDef, int> byClass)
        {
            var sb = new StringBuilder();
            bool first = true;
            foreach (var kv in byClass.OrderByDescending(k => k.Value))
            {
                if (!first) sb.Append(", ");
                first = false;
                sb.Append(kv.Value).Append(" ").Append(kv.Value == 1 ? kv.Key.label : kv.Key.label + "s");
            }
            return sb.ToString();
        }
    }
}
