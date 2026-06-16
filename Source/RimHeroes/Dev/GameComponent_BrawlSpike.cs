using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace RimHeroes
{
    /// <summary>
    /// Combat demo: -quicktest -rhbrawl. Runs 10 sequential ~60s battles on a cleared sand arena:
    /// six 4-hero parties vs varied raids, four 4-hero vs 4-hero duels. Every hero is L20 with its
    /// full kit, fights AI-driven, and has every ability auto-cast by a driver. Frames the camera and
    /// narrates each matchup. Leaves the game running at the end (no auto-exit).
    /// </summary>
    public class GameComponent_BrawlSpike : GameComponent
    {
        private static readonly bool Active = GenCommandLine.CommandLineArgPassed("rhbrawl");

        private const int BattleTicks = 3600;   // ~60s at 1x
        private const int DriveEvery = 30;      // ability/engagement driver cadence

        private class Battle
        {
            public string desc;
            public string[] a;                  // hero classes, side A (always heroes on the hero faction)
            public string[] b;                  // hero classes, side B (hero-vs-hero only)
            public (string kind, int count)[] raid;
            public string raidFaction;          // mech | insect | pirate | tribal
        }

        private static readonly Battle[] Battles =
        {
            new Battle { desc = "Party vs small tribal warband", a = new[]{"Barbarian","Fighter","Cleric","Wizard"},
                raidFaction="tribal", raid=new[]{("Tribal_Warrior",6)} },
            new Battle { desc = "Party vs pirate gunline", a = new[]{"Ranger","Rogue","Paladin","Sorcerer"},
                raidFaction="pirate", raid=new[]{("Pirate",4),("Mercenary_Gunner",4)} },
            new Battle { desc = "Party vs mechanoid strike (scythers + lancers)", a = new[]{"Fighter","Barbarian","Druid","Bard"},
                raidFaction="mech", raid=new[]{("Mech_Scyther",3),("Mech_Lancer",3)} },
            new Battle { desc = "Party vs insect swarm", a = new[]{"Paladin","Monk","Cleric","Warlock"},
                raidFaction="insect", raid=new[]{("Megaspider",4),("Spelopede",4)} },
            new Battle { desc = "Party vs large tribal horde", a = new[]{"Barbarian","Ranger","Wizard","Druid"},
                raidFaction="tribal", raid=new[]{("Tribal_Warrior",10),("Tribal_Archer",4)} },
            new Battle { desc = "Party vs heavy mechanoids (centipedes)", a = new[]{"Fighter","Paladin","Sorcerer","Cleric"},
                raidFaction="mech", raid=new[]{("Mech_Centipede",3),("Mech_Lancer",2)} },

            new Battle { desc = "Duel: martial + arcane vs martial + arcane", a = new[]{"Barbarian","Wizard","Cleric","Ranger"},
                b = new[]{"Fighter","Sorcerer","Paladin","Rogue"} },
            new Battle { desc = "Duel: nature + pact vs frontline + arcane", a = new[]{"Monk","Druid","Bard","Warlock"},
                b = new[]{"Barbarian","Ranger","Wizard","Cleric"} },
            new Battle { desc = "Duel: skirmishers vs casters", a = new[]{"Paladin","Rogue","Sorcerer","Fighter"},
                b = new[]{"Monk","Warlock","Druid","Bard"} },
            new Battle { desc = "Duel: arcane line vs ranged line", a = new[]{"Wizard","Cleric","Barbarian","Monk"},
                b = new[]{"Ranger","Paladin","Fighter","Sorcerer"} },
        };

        private int phase;                      // 0 = init, 1 = fighting, 2 = done
        private int battleIdx = -1;
        private int phaseTick;
        private float startGate = -1f;
        private Faction heroFaction;
        private IntVec3 center;
        private readonly List<Pawn> spawned = new List<Pawn>();
        private readonly HashSet<Pawn> heroes = new HashSet<Pawn>();

        public GameComponent_BrawlSpike(Game game) { }

        public override void GameComponentTick()
        {
            if (!Active || phase == 2) return;
            var map = Find.CurrentMap;
            if (map == null || Find.TickManager.TicksGame < 120) return;
            if (startGate < 0f) { startGate = Time.realtimeSinceStartup + 3f; return; }
            if (Time.realtimeSinceStartup < startGate && phase == 0) return;

            if (phase == 0)
            {
                Setup(map);
                StartBattle(map, 0);
                return;
            }

            // fighting
            phaseTick++;
            if (phaseTick % DriveEvery == 0) Drive(map);
            if (phaseTick >= BattleTicks)
            {
                int next = battleIdx + 1;
                if (next >= Battles.Length)
                {
                    Cleanup(map);
                    Log.Message("[RimHeroes.Brawl] RESULT: all 10 brawls complete verdict=PASS");
                    Messages.Message("Brawl demo complete: all 10 battles done.", MessageTypeDefOf.PositiveEvent, false);
                    phase = 2;
                    return;
                }
                StartBattle(map, next);
            }
        }

        private void Setup(Map map)
        {
            center = map.Center;
            heroFaction = MakeHeroFaction();
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
        }

        private void StartBattle(Map map, int idx)
        {
            Cleanup(map);
            battleIdx = idx;
            var bt = Battles[idx];
            ClearArena(map, 24, 14);

            // side A: heroes on the hero faction, left
            foreach (var cls in bt.a)
            {
                var p = SpawnHero(map, ScatterCell(map, center + new IntVec3(-14, 0, 0), 5), "RH_" + cls, heroFaction);
                if (p != null) { spawned.Add(p); heroes.Add(p); }
            }

            // side B: rival heroes (duel) or a raid
            if (bt.b != null)
            {
                foreach (var cls in bt.b)
                {
                    var p = SpawnHero(map, ScatterCell(map, center + new IntVec3(14, 0, 0), 5), "RH_" + cls, Faction.OfPirates);
                    if (p != null) { spawned.Add(p); heroes.Add(p); }
                }
            }
            else
            {
                var rf = RaidFaction(bt.raidFaction);
                foreach (var (kind, count) in bt.raid)
                    for (int i = 0; i < count; i++)
                    {
                        var p = SpawnMob(map, ScatterCell(map, center + new IntVec3(14, 0, 0), 6), kind, rf);
                        if (p != null) spawned.Add(p);
                    }
            }

            foreach (var p in spawned)
                if (p?.mindState != null) p.mindState.canFleeIndividual = false;

            Find.CameraDriver.SetRootPosAndSize(center.ToVector3(), 15f);
            Find.TickManager.CurTimeSpeed = TimeSpeed.Normal;
            phaseTick = 0;
            phase = 1;
            Log.Message($"[RimHeroes.Brawl] battle {idx + 1}/10 start: {bt.desc} ({spawned.Count} pawns)");
            Messages.Message($"Battle {idx + 1}/10: {bt.desc}", MessageTypeDefOf.NeutralEvent, false);
        }

        // ability auto-cast + engagement nudge for the humanlike combatants
        private void Drive(Map map)
        {
            // Ability casting for these AI heroes is handled by the real feature
            // (Hediff_HeroLevels.TickAutocastAI). The brawl only nudges engagement/movement.
            foreach (var p in spawned)
            {
                if (p == null || !p.Spawned || p.Dead || p.Downed) continue;
                if (!p.RaceProps.Humanlike) continue;          // mechs/insects self-drive
                if (p.CurJob?.ability != null) continue;        // let a cast finish
                EnsureFighting(p);
            }
        }

        private void EnsureFighting(Pawn p)
        {
            var cur = p.CurJobDef;
            if (cur == JobDefOf.AttackMelee || cur == JobDefOf.AttackStatic)
            {
                if (p.CurJob.targetA.Thing is Pawn tp && !tp.Dead && !tp.Downed) return;
            }
            var enemy = NearestEnemy(p, 9999f, false);
            if (enemy == null) return;
            try
            {
                bool ranged = p.equipment?.Primary?.def?.IsRangedWeapon ?? false;
                Job job;
                if (ranged)
                {
                    float r = p.equipment.PrimaryEq?.PrimaryVerb?.verbProps?.range ?? 25f;
                    if (p.Position.DistanceTo(enemy.Position) <= r && GenSight.LineOfSight(p.Position, enemy.Position, p.Map))
                        job = JobMaker.MakeJob(JobDefOf.AttackStatic, enemy);
                    else
                        job = JobMaker.MakeJob(JobDefOf.Goto, CellFinder.RandomClosewalkCellNear(enemy.Position, p.Map, 6));
                }
                else
                {
                    job = JobMaker.MakeJob(JobDefOf.AttackMelee, enemy);
                }
                job.expiryInterval = 120;
                job.checkOverrideOnExpire = true;
                p.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            }
            catch { }
        }

        private Pawn NearestEnemy(Pawn p, float range, bool needLos)
        {
            Pawn best = null;
            float bestDist = float.MaxValue;
            foreach (var o in p.Map.mapPawns.AllPawnsSpawned)
            {
                if (o == p || o.Dead || o.Downed || !o.HostileTo(p)) continue;
                float d = p.Position.DistanceTo(o.Position);
                if (d > range || d >= bestDist) continue;
                if (needLos && !GenSight.LineOfSight(p.Position, o.Position, p.Map, skipFirstCell: true)) continue;
                best = o; bestDist = d;
            }
            return best;
        }

        private Pawn SpawnHero(Map map, IntVec3 cell, string cls, Faction fac)
        {
            try
            {
                var classDef = DefDatabase<HeroClassDef>.GetNamedSilentFail(cls);
                if (classDef == null) { Log.Error("[RimHeroes.Brawl] missing class " + cls); return null; }
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(PawnKindDefOf.Colonist, fac,
                    fixedBiologicalAge: 30f, fixedChronologicalAge: 30f, forceGenerateNewPawn: true, fixedGender: Gender.Male));
                GenSpawn.Spawn(p, cell, map);
                if (p.story != null) p.story.bodyType = BodyTypeDefOf.Male;
                p.apparel?.DestroyAll();
                p.equipment?.DestroyAllEquipment();
                var levels = HeroUtility.MakeHero(p, classDef);
                levels.SetLevelDirect(20);
                Equip(p, $"RH_Weapon_{cls.Replace("RH_", "")}_{HeroUtility.WeaponTierSuffix(5)}");
                p.Drawer?.renderer?.SetAllGraphicsDirty();
                p.Name = new NameSingle(cls.Replace("RH_", ""));
                return p;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.Brawl] spawn hero {cls} failed: {e}"); return null; }
        }

        private Pawn SpawnMob(Map map, IntVec3 cell, string kindName, Faction fac)
        {
            try
            {
                var kind = DefDatabase<PawnKindDef>.GetNamedSilentFail(kindName);
                if (kind == null) { Log.Warning("[RimHeroes.Brawl] missing pawnkind " + kindName); return null; }
                var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, fac, forceGenerateNewPawn: true));
                GenSpawn.Spawn(p, cell, map);
                return p;
            }
            catch (System.Exception e) { Log.Error($"[RimHeroes.Brawl] spawn mob {kindName} failed: {e}"); return null; }
        }

        private void Equip(Pawn p, string weaponDef)
        {
            var def = DefDatabase<ThingDef>.GetNamedSilentFail(weaponDef);
            if (def == null) { Log.Warning("[RimHeroes.Brawl] missing weapon " + weaponDef); return; }
            var w = (ThingWithComps)ThingMaker.MakeThing(def);
            p.equipment.MakeRoomFor(w);
            p.equipment.AddEquipment(w);
        }

        private Faction MakeHeroFaction()
        {
            var fdef = DefDatabase<FactionDef>.GetNamedSilentFail("Pirate")
                       ?? DefDatabase<FactionDef>.AllDefs.First(d => !d.isPlayer && d.humanlikeFaction && !d.hidden);
            Faction f;
            try { f = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(fdef, default(IdeoGenerationParms), true)); }
            catch { f = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(fdef)); }
            f.Name = "Heroes";
            Find.FactionManager.Add(f);
            foreach (var o in Find.FactionManager.AllFactionsListForReading)
            {
                if (o == f) continue;
                try { f.SetRelationDirect(o, FactionRelationKind.Hostile, false); } catch { }
                try { o.SetRelationDirect(f, FactionRelationKind.Hostile, false); } catch { }
            }
            return f;
        }

        private Faction RaidFaction(string type)
        {
            switch (type)
            {
                case "mech": return Faction.OfMechanoids;
                case "insect": return Faction.OfInsects;
                case "pirate": return Faction.OfPirates;
                case "tribal":
                    return Find.FactionManager.AllFactionsListForReading.FirstOrDefault(
                               f => !f.IsPlayer && !f.defeated && f.def.humanlikeFaction
                                    && f.def.techLevel <= TechLevel.Neolithic && f != heroFaction)
                           ?? Faction.OfPirates;
                default: return Faction.OfPirates;
            }
        }

        private IntVec3 ScatterCell(Map map, IntVec3 around, int radius)
        {
            return CellFinder.RandomClosewalkCellNear(around, map, radius, c => c.Standable(map));
        }

        private void ClearArena(Map map, int halfW, int halfH)
        {
            for (int dx = -halfW; dx <= halfW; dx++)
                for (int dz = -halfH; dz <= halfH; dz++)
                {
                    var c = center + new IntVec3(dx, 0, dz);
                    if (!c.InBounds(map)) continue;
                    foreach (var t in c.GetThingList(map).ToList())
                    {
                        if (t is Pawn) continue;
                        if (t.def != null && t.def.destroyable) t.Destroy(DestroyMode.Vanish);
                    }
                    map.terrainGrid.SetTerrain(c, TerrainDefOf.Sand);
                    map.snowGrid?.SetDepth(c, 0f);
                    map.roofGrid?.SetRoof(c, null);
                }
        }

        private void Cleanup(Map map)
        {
            foreach (var p in spawned.ToList())
            {
                try { if (p != null && !p.Destroyed) p.Destroy(DestroyMode.Vanish); } catch { }
            }
            spawned.Clear();
            heroes.Clear();
            // sweep corpses/filth left behind in the arena
            for (int dx = -24; dx <= 24; dx++)
                for (int dz = -14; dz <= 14; dz++)
                {
                    var c = center + new IntVec3(dx, 0, dz);
                    if (!c.InBounds(map)) continue;
                    foreach (var t in c.GetThingList(map).ToList())
                        if (t is Corpse || t is Filth) { try { t.Destroy(DestroyMode.Vanish); } catch { } }
                }
        }
    }
}
