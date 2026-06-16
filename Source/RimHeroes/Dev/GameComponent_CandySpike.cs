using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Exp-candy check: -quicktest -rhcandy. Confirms the four candy defs load, a hero who eats one
    /// gains XP (and levels), and a non-hero is refused. Logs assertions + a verdict, then exits.
    /// </summary>
    public class GameComponent_CandySpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhcandy");
        private bool done;
        private float gate = -1f;
        private bool ok = true;

        public GameComponent_CandySpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.Candy] === exp candy ===");
            foreach (var n in new[] { "RH_ExpCandy_S", "RH_ExpCandy_M", "RH_ExpCandy_L", "RH_ExpCandy_XL" })
                Assert(DefDatabase<ThingDef>.GetNamedSilentFail(n) != null, $"{n} loads");

            // a hero eats an L candy and levels up
            var hero = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true));
            GenSpawn.Spawn(hero, map.Center, map);
            var levels = HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Fighter"));
            int beforeLevel = levels.level;
            var candy = ThingMaker.MakeThing(DefDatabase<ThingDef>.GetNamed("RH_ExpCandy_L"));
            var comp = candy.TryGetComp<CompUseEffect_GrantHeroXP>();
            Assert(comp != null && comp.CanBeUsedBy(hero).Accepted, "a hero can use the candy");
            comp?.DoEffect(hero);
            Assert(levels.level > beforeLevel, $"eating L candy (1500xp) leveled the hero {beforeLevel}->{levels.level}");

            // a non-hero is refused
            var pleb = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true));
            GenSpawn.Spawn(pleb, map.Center, map);
            Assert(comp != null && !comp.CanBeUsedBy(pleb).Accepted, "a non-hero is refused");

            Log.Message($"[RimHeroes.Candy] RESULT: exp candy verdict={(ok ? "PASS" : "FAIL")}");
            Root.Shutdown();
        }

        private void Assert(bool cond, string what)
        {
            if (cond) Log.Message($"[RimHeroes.Candy] OK: {what}");
            else { Log.Warning($"[RimHeroes.Candy] FAIL: {what}"); ok = false; }
        }
    }
}
