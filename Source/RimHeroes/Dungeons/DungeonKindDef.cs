using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A themed dungeon: the data that drives one instanced delve. The generic GenSteps read this to
    /// theme the rock/floor/light, stock the rooms from a monster pool, place the boss + vault loot,
    /// and flavour the entrance. Signature hazards mostly come free from the monsters themselves
    /// (Menagerie's basilisks/medusae petrify, beholders fire eye-rays, rust monsters corrode gear).
    /// </summary>
    public class DungeonKindDef : Def
    {
        // ===== theme =====
        public ThingDef rockDef;                 // wall rock (default Granite)
        public TerrainDef floorDef;              // default AncientTile
        public ThingDef brazierDef;              // light source (default RH_Brazier)
        public float temperature = 12f;

        // ===== boss presentation =====
        public float bossScale = 1.35f;          // render scale so the boss looms over its kin
        public Color bossAura = Color.clear;     // magical glow behind the boss (clear = none)

        // ===== population =====
        public List<DungeonMonster> monsters = new List<DungeonMonster>();
        public IntRange perRoomMonsters = new IntRange(2, 4);
        public DungeonBossSpec boss;
        public int bossGuards = 2;
        public bool useTraps = true;
        public int trapCount = 3;
        public int chestCount = 2;

        // ===== vault loot (on top of the base silver/medicine + the reliquary's inlay) =====
        public List<DungeonLoot> vaultLoot = new List<DungeonLoot>();

        // ===== set dressing =====
        public string entranceTexPath;           // per-theme entrance sprite (default the crypt stairway)
        public List<ThingDef> props = new List<ThingDef>();  // decorative theme props scattered in rooms
        public IntRange propsPerRoom = new IntRange(0, 2);

        // ===== entrance / incident =====
        public string entranceLabel;             // letter label when the entrance appears
        public string entranceText;              // letter body
        public float incidentCommonality = 1f;   // relative weight when an entrance is rolled

        public ThingDef RandomProp() => props.NullOrEmpty() ? null : props.RandomElement();

        public PawnKindDef RandomMonster()
        {
            if (monsters.NullOrEmpty()) return null;
            return monsters.RandomElementByWeight(m => m.weight)?.kind;
        }
    }

    public class DungeonMonster
    {
        public PawnKindDef kind;
        public float weight = 1f;
    }

    public class DungeonLoot
    {
        public ThingDef thing;
        public IntRange count = new IntRange(1, 1);
        public float chance = 1f;
    }

    /// <summary>How to build a dungeon's boss. Either a Menagerie/animal pawnkind dressed up, a hero
    /// boss (our own class, so it fights with the enemy-hero autocast), or the crypt-lord recipe
    /// (humanlike + mutant + desecrated vestment).</summary>
    public class DungeonBossSpec
    {
        public PawnKindDef kind;            // base pawnkind (a Menagerie monster, or Colonist for hero/crypt-lord)
        public MutantDef mutant;            // optional: turn it into a mutant (crypt lord = Shambler)
        public string vestmentHediff;       // optional: add a class vestment hediff (crypt lord = RH_Vestment_Fighter)
        public bool desecrated = true;      // dark-tint the vestment
        public string weapon;               // optional: equip this weapon def
        public string heroClass;            // optional: make it a level-20 enemy hero of this class
        public string label = "dungeon lord";
    }
}
