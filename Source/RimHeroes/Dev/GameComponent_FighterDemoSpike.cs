using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Fighter progression check: -quicktest -rhfighterdemo. Spawns Fighters at L1/5/11/20 and logs
    /// the class-features hediff (label + tooltip), the derived melee/toughness numbers, and the
    /// granted martial abilities, so the level-up assignment can be eyeballed. Then exits.
    /// </summary>
    public class GameComponent_FighterDemoSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhfighterdemo");
        private int state;
        private float nextStateTime = -1f;

        public GameComponent_FighterDemoSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 1) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f) { nextStateTime = now + 4f; return; }
            if (now < nextStateTime) return;
            try { RunDemo(map); }
            catch (System.Exception e) { Log.Error("[RimHeroes.FighterDemo] " + e); }
            state = 2;
            Root.Shutdown();
        }

        private void RunDemo(Map map)
        {
            bool pass = true;
            foreach (int lvl in new[] { 1, 5, 11, 20 })
            {
                CellFinder.TryFindRandomCellNear(map.Center, map, 12, c => c.Standable(map), out var cell);
                var hero = SpawnFighter(map, cell, lvl);
                var cf = hero.pawn.health.hediffSet.GetFirstHediffOfDef(RH_DefOf.RH_ClassFeatures);
                string label = cf?.Label ?? "(none)";
                bool hasSecondWind = HasAbility(hero.pawn, "RH_Ability_SecondWind");
                bool hasActionSurge = HasAbility(hero.pawn, "RH_Ability_ActionSurge");
                Log.Message($"[RimHeroes.FighterDemo] L{lvl}: classHediff='{label}' style={hero.FightingStyle} " +
                            $"meleeDmgx{ClassFeatures.MeleeDamageFactor(hero):F2} cooldownx{ClassFeatures.MeleeCooldownFactor(hero):F2} " +
                            $"armorPen+{ClassFeatures.MeleeArmorPenOffset(hero):F2} extraAtkT{ClassFeatures.ExtraAttackTier(lvl)} " +
                            $"indomT{ClassFeatures.IndomitableTier(lvl)} deathSave+{hero.DeathSaveBonus} " +
                            $"secondWind={hasSecondWind} actionSurge={hasActionSurge}");
                Log.Message($"[RimHeroes.FighterDemo]   tooltip: " + (cf?.TipStringExtra ?? "").Replace("\r", "").Replace("\n", " | "));
                Log.Message($"[RimHeroes.FighterDemo]   stats: move={hero.pawn.GetStatValue(StatDefOf.MoveSpeed):F2} " +
                            $"workx{hero.pawn.GetStatValue(StatDefOf.WorkSpeedGlobal):F2} painThresh={hero.pawn.GetStatValue(StatDefOf.PainShockThreshold):F2}");

                if (cf == null || label != "Fighter") pass = false;
                if (hero.FightingStyle == FighterStyle.None) pass = false;
                if (!hasSecondWind) pass = false;
                if (lvl >= 2 && !hasActionSurge) pass = false;
                if (lvl >= 5 && ClassFeatures.MeleeDamageFactor(hero) <= 1f) pass = false;
            }
            Log.Message($"[RimHeroes.FighterDemo] RESULT: verdict={(pass ? "PASS" : "FAIL")}");
        }

        private static bool HasAbility(Pawn p, string defName) =>
            p.abilities?.abilities?.Any(a => a.def.defName == defName) == true;

        private Hediff_HeroLevels SpawnFighter(Map map, IntVec3 cell, int lvl)
        {
            var cd = DefDatabase<HeroClassDef>.GetNamed("RH_Fighter");
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist,
                Faction.OfPlayer, forceGenerateNewPawn: true, fixedGender: Gender.Male));
            GenSpawn.Spawn(p, cell, map);
            var lv = HeroUtility.MakeHero(p, cd);
            lv.SetLevelDirect(lvl);
            return lv;
        }
    }
}
