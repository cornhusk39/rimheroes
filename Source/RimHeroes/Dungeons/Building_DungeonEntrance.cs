using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace RimHeroes
{
    /// <summary>
    /// A dungeon entrance the party delves through. A MapPortal that remembers which DungeonKind it
    /// leads to (read off PocketMapUtility.currentlyGeneratingPortal while the pocket map is built).
    ///
    /// While it stands on the colony map it slowly leaks that dungeon's monsters: a trickle at first,
    /// ramping over about a month into a real threat, faster the richer the colony. Damaging it stirs
    /// the nest and speeds the spawns. It stops when the party delves in and kills the boss (sealed via
    /// the boss-death patch) or when the entrance itself is torn down.
    /// </summary>
    public class Building_DungeonEntrance : MapPortal
    {
        public DungeonKindDef kind;
        public int tier = 1;
        public float difficulty = 1f;

        private bool sealedShut;
        private int spawnTick;
        private int lastDamageTick = -999999;
        private int nextSpawnTick;
        private bool warned;

        public bool IsSealed => sealedShut;
        private float AgeDays => (GenTicks.TicksGame - spawnTick) / 60000f;

        // Dev-only overrides so spikes can generate a chosen kind/tier without a live portal-enter flow.
        public static DungeonKindDef DebugForcedKind;
        public static int DebugForcedTier = 1;
        public static float DebugForcedDifficulty = 1f;

        public static Building_DungeonEntrance Generating =>
            PocketMapUtility.currentlyGeneratingPortal as Building_DungeonEntrance;

        public static DungeonKindDef GeneratingKind =>
            DebugForcedKind
            ?? Generating?.kind
            ?? DefDatabase<DungeonKindDef>.GetNamedSilentFail("RH_Dungeon_Crypt");

        public override string Label => kind != null ? kind.LabelCap : base.Label;

        private Graphic cachedGraphic;

        // Per-theme entrance sprite, when the kind specifies one; otherwise the default crypt stairway.
        public override Graphic Graphic
        {
            get
            {
                if (kind == null || kind.entranceTexPath.NullOrEmpty()) return base.Graphic;
                if (cachedGraphic == null)
                    cachedGraphic = GraphicDatabase.Get<Graphic_Single>(
                        kind.entranceTexPath, ShaderDatabase.Cutout, def.graphicData.drawSize, Color.white);
                return cachedGraphic;
            }
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                spawnTick = GenTicks.TicksGame;
                nextSpawnTick = GenTicks.TicksGame + SpawnIntervalTicks();
            }
        }

        public override void Tick()
        {
            base.Tick();
            if (sealedShut || kind == null || Map == null || Map.IsPocketMap) return;
            if (GenTicks.TicksGame >= nextSpawnTick)
            {
                SpawnTrickle();
                nextSpawnTick = GenTicks.TicksGame + SpawnIntervalTicks();
            }
        }

        /// <summary>Ticks until the next spawn: ~1.5 days when fresh, shrinking to ~3 hours after a
        /// month, divided by the colony-scaled difficulty, and cut sharply for a while after damage.</summary>
        public int SpawnIntervalTicks()
        {
            float interval = Mathf.Lerp(90000f, 7500f, Mathf.Clamp01(AgeDays / 30f));
            interval /= Mathf.Max(0.8f, difficulty);
            if (GenTicks.TicksGame - lastDamageTick < 5000) interval *= 0.4f;
            return Mathf.Max(2500, Mathf.RoundToInt(interval * Rand.Range(0.85f, 1.15f)));
        }

        /// <summary>Leak one (or, once it has festered a while, two) of the dungeon's monsters and set
        /// them assaulting the colony.</summary>
        public void SpawnTrickle()
        {
            var map = Map;
            if (sealedShut || map == null || kind == null) return;
            var faction = Faction.OfEntities;
            int n = AgeDays > 20f ? 2 : 1;
            var spawned = new List<Pawn>();
            for (int i = 0; i < n; i++)
            {
                var monster = kind.RandomMonster();
                if (monster == null) break;
                if (!CellFinder.TryFindRandomCellNear(Position, map, 5,
                        c => c.Standable(map) && c.GetEdifice(map) == null && !c.Fogged(map), out var cell)) break;
                try
                {
                    var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(monster, faction, forceGenerateNewPawn: true));
                    GenSpawn.Spawn(p, cell, map);
                    spawned.Add(p);
                }
                catch (System.Exception e) { Log.Warning($"[RimHeroes] trickle spawn failed: {e.Message}"); }
            }
            if (spawned.Count == 0) return;
            try { LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction), map, spawned); }
            catch (System.Exception e) { Log.Warning($"[RimHeroes] trickle lord failed: {e.Message}"); }

            if (!warned)
            {
                warned = true;
                Messages.Message("RH_TrickleStart".Translate(Label), new LookTargets(this), MessageTypeDefOf.ThreatSmall);
            }
        }

        public override void PostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.PostApplyDamage(dinfo, totalDamageDealt);
            lastDamageTick = GenTicks.TicksGame;
        }

        /// <summary>Stop the trickle for good (the dungeon's boss is dead, or the player tore the door down).</summary>
        public void Seal()
        {
            if (sealedShut) return;
            sealedShut = true;
            if (Spawned) Messages.Message("RH_DungeonSealed".Translate(Label), new LookTargets(this), MessageTypeDefOf.PositiveEvent);
        }

        public override string GetInspectString()
        {
            var sb = new System.Text.StringBuilder(base.GetInspectString());
            if (sb.Length > 0) sb.AppendLine();
            sb.Append(sealedShut ? "RH_EntranceSealed".Translate() : "RH_EntranceActive".Translate());
            return sb.ToString();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Defs.Look(ref kind, "kind");
            Scribe_Values.Look(ref tier, "tier", 1);
            Scribe_Values.Look(ref difficulty, "difficulty", 1f);
            Scribe_Values.Look(ref sealedShut, "sealedShut");
            Scribe_Values.Look(ref spawnTick, "spawnTick");
            Scribe_Values.Look(ref lastDamageTick, "lastDamageTick", -999999);
            Scribe_Values.Look(ref nextSpawnTick, "nextSpawnTick");
            Scribe_Values.Look(ref warned, "warned");
        }
    }
}
