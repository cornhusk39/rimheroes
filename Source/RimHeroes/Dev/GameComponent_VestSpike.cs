using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Automated vestment test: launch with -quicktest -rhvestspike.
    /// Verifies: vestment granted at tier 1 with armor stats; tier rises with level and armor
    /// scales; torso armor blocked while pants/shirts stay wearable; enhancement surgery only
    /// available on vestment-bearers and its hediff adds armor. Screenshots the tinted visual.
    /// </summary>
    public class GameComponent_VestSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhvestspike");

        private int state;
        private float nextStateTime = -1f;
        private Pawn hero;
        private float armorT1;
        private bool passGrant, passTier, passBlock, passEnh;

        public GameComponent_VestSpike(Game game) { }

        public override void GameComponentUpdate()
        {
            if (!Active || state > 5)
            {
                return;
            }
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120)
            {
                return;
            }
            float now = Time.realtimeSinceStartup;
            if (nextStateTime < 0f)
            {
                nextStateTime = now + 4f;
                return;
            }
            if (now < nextStateTime)
            {
                return;
            }
            nextStateTime = now + 3f;

            switch (state)
            {
                case 0:
                {
                    hero = map.mapPawns.FreeColonistsSpawned.FirstOrDefault();
                    if (hero == null)
                    {
                        hero = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
                        GenSpawn.Spawn(hero, map.Center, map);
                    }
                    var levels = HeroUtility.MakeHero(hero, DefDatabase<HeroClassDef>.GetNamed("RH_Fighter"));
                    var vestment = levels.Vestment;
                    armorT1 = hero.GetStatValue(StatDefOf.ArmorRating_Sharp);
                    passGrant = vestment != null && vestment.Tier == 1 && armorT1 > 0.15f;
                    Log.Message($"[RimHeroes.VestSpike] grant: vestment={(vestment != null)} tier={vestment?.Tier} sharp={armorT1:F2} pass={passGrant}");
                    state = 1;
                    break;
                }
                case 1:
                {
                    var levels = HeroUtility.GetHeroHediff(hero);
                    levels.GainXP(600f); // -> L5 -> tier 2
                    float armorT2 = hero.GetStatValue(StatDefOf.ArmorRating_Sharp);
                    passTier = levels.Vestment?.Tier == 2 && armorT2 > armorT1 + 0.1f;
                    Log.Message($"[RimHeroes.VestSpike] tier: level={levels.level} tier={levels.Vestment?.Tier} sharp={armorT2:F2} pass={passTier}");
                    state = 2;
                    break;
                }
                case 2:
                {
                    bool flakBlocked = !ApparelUtility.HasPartsToWear(hero, ThingDef.Named("Apparel_FlakVest"));
                    bool pantsOk = ApparelUtility.HasPartsToWear(hero, ThingDef.Named("Apparel_Pants"));
                    bool shirtOk = ApparelUtility.HasPartsToWear(hero, ThingDef.Named("Apparel_BasicShirt"));
                    passBlock = flakBlocked && pantsOk && shirtOk;
                    Log.Message($"[RimHeroes.VestSpike] block: flakBlocked={flakBlocked} pantsOk={pantsOk} shirtOk={shirtOk} pass={passBlock}");
                    state = 3;
                    break;
                }
                case 3:
                {
                    var lesserRecipe = DefDatabase<RecipeDef>.GetNamed("RH_Install_Warding_Lesser");
                    bool onHero = lesserRecipe.Worker.AvailableOnNow(hero);
                    var commoner = map.mapPawns.AllPawnsSpawned.FirstOrDefault(p => p.RaceProps.Humanlike && p != hero && !HeroUtility.IsHero(p));
                    bool onCommoner = commoner != null && lesserRecipe.Worker.AvailableOnNow(commoner);

                    float blunt0 = hero.GetStatValue(StatDefOf.ArmorRating_Blunt);
                    InlayUtility.Install(hero, HediffDef.Named("RH_Inlay_Warding_Lesser"));
                    bool dupBlocked = !lesserRecipe.Worker.AvailableOnNow(hero); // identical inlay re-install blocked
                    InlayUtility.Install(hero, HediffDef.Named("RH_Inlay_Warding_Regular")); // same slot -> replaces
                    bool lesserGone = !hero.health.hediffSet.HasHediff(HediffDef.Named("RH_Inlay_Warding_Lesser"));
                    bool regularIn = hero.health.hediffSet.HasHediff(HediffDef.Named("RH_Inlay_Warding_Regular"));
                    InlayUtility.Install(hero, HediffDef.Named("RH_Inlay_Keenness_Greater"));
                    InlayUtility.Install(hero, HediffDef.Named("RH_Inlay_Fleetness_Regular"));
                    int installed = InlayUtility.InstalledInlays(hero).Count();
                    float blunt1 = hero.GetStatValue(StatDefOf.ArmorRating_Blunt);

                    passEnh = onHero && !onCommoner && dupBlocked && lesserGone && regularIn && installed == 3 && blunt1 > blunt0 + 0.1f;
                    Log.Message($"[RimHeroes.VestSpike] inlays: onHero={onHero} onCommoner={onCommoner}(commoner={(commoner != null)}) dupBlocked={dupBlocked} replaced={lesserGone && regularIn} slots={installed}/3 blunt {blunt0:F2}->{blunt1:F2} pass={passEnh}");
                    Find.Selector.ClearSelection();
                    Find.Selector.Select(hero);
                    Find.CameraDriver.SetRootPosAndSize(hero.DrawPos, 12f);
                    state = 4;
                    break;
                }
                case 4:
                {
                    ScreenshotTaker.TakeNonSteamShot("rhvestspike");
                    string verdict = passGrant && passTier && passBlock && passEnh ? "PASS" : "FAIL";
                    Log.Message($"[RimHeroes.VestSpike] RESULT: grant={passGrant} tier={passTier} block={passBlock} enh={passEnh} verdict={verdict}");
                    state = 5;
                    break;
                }
                case 5: // extra frame so the async screenshot flushes before shutdown
                    state = 6;
                    Root.Shutdown();
                    break;
            }
        }
    }
}
