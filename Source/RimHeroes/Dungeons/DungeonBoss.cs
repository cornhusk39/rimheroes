using System.Linq;
using RimWorld;
using Verse;

namespace RimHeroes
{
    /// <summary>Builds a dungeon boss from a DungeonBossSpec. Three shapes: a hero boss (our own class,
    /// so it fights with the enemy-hero autocast), the crypt-lord recipe (humanlike + Shambler mutant +
    /// desecrated vestment), or a plain Menagerie/animal pawnkind dressed up. Every boss gets the
    /// RH_DungeonBoss buff so it reads as a boss, not just a bigger mook.</summary>
    public static class DungeonBoss
    {
        public static Pawn Spawn(Map map, IntVec3 cell, Faction faction, DungeonKindDef kind)
        {
            var spec = kind?.boss;
            if (spec == null) return null;
            try
            {
                Pawn boss = !spec.heroClass.NullOrEmpty()
                    ? SpawnHeroBoss(map, cell, faction, spec)
                    : SpawnMonsterBoss(map, cell, faction, spec);
                if (boss == null) return null;

                if (!spec.label.NullOrEmpty()) boss.Name = new NameSingle(spec.label);
                var buff = DefDatabase<HediffDef>.GetNamedSilentFail("RH_DungeonBoss");
                if (buff != null && boss.health.hediffSet.GetFirstHediffOfDef(buff) == null
                    && boss.health.AddHediff(buff) is Hediff_DungeonBoss bossHediff)
                {
                    bossHediff.scale = kind.bossScale;
                    bossHediff.aura = kind.bossAura;
                }

                // 5% chance to carry a heroic blessing, dropped with its body on death.
                if (Rand.Chance(0.05f))
                {
                    var blessing = DefDatabase<ThingDef>.GetNamedSilentFail("RH_HeroicBlessing");
                    if (blessing != null && boss.inventory != null)
                        boss.inventory.innerContainer.TryAdd(ThingMaker.MakeThing(blessing));
                }

                boss.Drawer?.renderer?.renderTree?.SetDirty();
                boss.Drawer?.renderer?.SetAllGraphicsDirty();
                return boss;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes] dungeon boss spawn failed: {e}"); return null; }
        }

        private static Pawn SpawnMonsterBoss(Map map, IntVec3 cell, Faction faction, DungeonBossSpec spec)
        {
            var kind = spec.kind ?? PawnKindDefOf.Colonist;
            var boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, faction, forceGenerateNewPawn: true));
            GenSpawn.Spawn(boss, cell, map);

            if (spec.mutant != null)
            {
                try { MutantUtility.SetPawnAsMutantInstantly(boss, spec.mutant); }
                catch (System.Exception e) { Log.Warning($"[RimHeroes] boss mutant failed: {e.Message}"); }
            }
            if (!spec.vestmentHediff.NullOrEmpty())
            {
                boss.apparel?.DestroyAll();
                var vestDef = DefDatabase<HediffDef>.GetNamedSilentFail(spec.vestmentHediff);
                if (vestDef != null && boss.health.AddHediff(vestDef) is Hediff_ClassVestment vest)
                {
                    vest.Severity = 5f;
                    vest.desecrated = spec.desecrated;
                }
            }
            EquipWeapon(boss, spec.weapon);
            return boss;
        }

        private static Pawn SpawnHeroBoss(Map map, IntVec3 cell, Faction faction, DungeonBossSpec spec)
        {
            var boss = PawnGenerator.GeneratePawn(new PawnGenerationRequest(spec.kind ?? PawnKindDefOf.Colonist, faction, forceGenerateNewPawn: true));
            GenSpawn.Spawn(boss, cell, map);
            var classDef = DefDatabase<HeroClassDef>.GetNamedSilentFail(spec.heroClass);
            if (classDef != null)
            {
                var levels = HeroUtility.MakeHero(boss, classDef);
                levels?.GainXP(200000f);   // drive it to the level cap so it has its full kit
            }
            EquipWeapon(boss, spec.weapon);
            return boss;
        }

        private static void EquipWeapon(Pawn boss, string weaponDefName)
        {
            if (weaponDefName.NullOrEmpty() || boss.equipment == null) return;
            var wpn = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDefName);
            if (wpn == null) return;
            var w = (ThingWithComps)ThingMaker.MakeThing(wpn);
            boss.equipment.MakeRoomFor(w);
            boss.equipment.AddEquipment(w);
        }
    }
}
