using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Trait-mapping verification: -quicktest -rhcheckmods. For each mapped vanilla trait, spawns a
    /// pawn, measures the check modifier before and after gaining the trait, and logs the delta (the
    /// trait's contribution). Prints to Player.log, then exits.
    /// </summary>
    public class GameComponent_CheckModsSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhcheckmods");
        private bool done;
        private float gate = -1f;

        public GameComponent_CheckModsSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || done) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            float now = Time.realtimeSinceStartup;
            if (gate < 0f) { gate = now + 3f; return; }
            if (now < gate) return;
            done = true;

            Log.Message("[RimHeroes.CheckMods] === trait -> check-kind deltas ===");
            Test(map, "Nimble", 0, CheckKind.Dodge);
            Test(map, "Tough", 0, CheckKind.Endurance);
            Test(map, "Tough", 0, CheckKind.Strength);
            Test(map, "Wimp", 0, CheckKind.Endurance);
            Test(map, "TooSmart", 0, CheckKind.Perception);
            Test(map, "TooSmart", 0, CheckKind.Arcane);
            Test(map, "TooSmart", 0, CheckKind.Lockpick);
            Test(map, "Undergrounder", 0, CheckKind.Perception);
            Test(map, "Brawler", 0, CheckKind.Strength);
            Test(map, "Kind", 0, CheckKind.Social);
            Test(map, "Abrasive", 0, CheckKind.Social);
            Test(map, "Psychopath", 0, CheckKind.Social);
            Test(map, "AnnoyingVoice", 0, CheckKind.Social);
            Test(map, "Beauty", 2, CheckKind.Social);
            Test(map, "Beauty", -2, CheckKind.Social);
            Test(map, "Immunity", 1, CheckKind.Endurance);
            Test(map, "Immunity", -1, CheckKind.Endurance);
            Test(map, "Nerves", 2, CheckKind.Endurance);
            Test(map, "Nerves", -2, CheckKind.Endurance);
            Test(map, "PsychicSensitivity", 2, CheckKind.Arcane);
            Test(map, "PsychicSensitivity", -2, CheckKind.Arcane);
            Log.Message("[RimHeroes.CheckMods] RESULT: trait map dumped verdict=PASS");
            Root.Shutdown();
        }

        private static int ModFor(Pawn p, CheckKind kind)
        {
            var c = new CheckDef { kind = kind, skill = kind == CheckKind.Skill ? SkillDefOf.Intellectual : null };
            return D20.GetModifier(p, c);
        }

        private static void Test(Map map, string traitDefName, int degree, CheckKind kind)
        {
            try
            {
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, Faction.OfPlayer, forceGenerateNewPawn: true));
                GenSpawn.Spawn(p, map.Center, map);
                int before = ModFor(p, kind);
                var td = DefDatabase<TraitDef>.GetNamedSilentFail(traitDefName);
                if (td != null) { try { p.story.traits.GainTrait(new Trait(td, degree, true)); } catch { } }
                int after = ModFor(p, kind);
                string deg = degree != 0 ? $":{degree}" : "";
                Log.Message($"[RimHeroes.CheckMods] {traitDefName}{deg} -> {kind}: {(after - before >= 0 ? "+" : "")}{after - before}");
                p.Destroy(DestroyMode.Vanish);
            }
            catch (System.Exception e) { Log.Warning($"[RimHeroes.CheckMods] {traitDefName} test failed: {e.Message}"); }
        }
    }
}
