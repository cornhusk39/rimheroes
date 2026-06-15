using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>Builds dungeon bosses. The Crypt Lord is a humanlike clad in a desecrated Fighter
    /// vestment (so the vestment art renders), turned shambler-grey via the Anomaly Shambler mutant,
    /// wielding the capstone fighter weapon.</summary>
    public static class DungeonBoss
    {
        public static Pawn SpawnCryptLord(Map map, IntVec3 cell, Faction faction)
        {
            try
            {
                var boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, faction,
                    fixedBiologicalAge: 45f, fixedChronologicalAge: 45f, forceGenerateNewPawn: true, fixedGender: Gender.Male));
                GenSpawn.Spawn(boss, cell, map);
                boss.apparel?.DestroyAll();
                boss.equipment?.DestroyAllEquipment();

                // shambler-grey look first, then the vestment on top (added pre-first-draw so it renders)
                var mutant = DefDatabase<MutantDef>.GetNamedSilentFail("Shambler");
                if (mutant != null)
                {
                    try { MutantUtility.SetPawnAsMutantInstantly(boss, mutant); } catch (System.Exception e) { Log.Warning($"[RimHeroes] shambler mutant failed: {e.Message}"); }
                }

                var vestDef = DefDatabase<HediffDef>.GetNamedSilentFail("RH_Vestment_Fighter");
                if (vestDef != null && boss.health.AddHediff(vestDef) is Hediff_ClassVestment vest)
                {
                    vest.Severity = 5f;
                    vest.desecrated = true;
                }

                var wpn = DefDatabase<ThingDef>.GetNamedSilentFail("RH_Weapon_Fighter_T5");
                if (wpn != null && boss.equipment != null)
                {
                    var w = (ThingWithComps)ThingMaker.MakeThing(wpn);
                    boss.equipment.MakeRoomFor(w);
                    boss.equipment.AddEquipment(w);
                }

                // 25% chance to carry a reliquary key, dropped with its body on death
                if (Rand.Chance(0.25f))
                {
                    var keyDef = DefDatabase<ThingDef>.GetNamedSilentFail("RH_ReliquaryKey");
                    if (keyDef != null && boss.inventory != null)
                        boss.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(keyDef));
                }

                boss.Name = new NameSingle("Crypt Lord");
                boss.Drawer?.renderer?.renderTree?.SetDirty();
                boss.Drawer?.renderer?.SetAllGraphicsDirty();
                return boss;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes] Crypt Lord spawn failed: {e}"); return null; }
        }
    }
}
