using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// A glowing seal painted on the boss-vault floor, tinted to the dungeon's theme colour. Pure
    /// decoration: walk-through, indestructible, drawn under the pawns. Its colour is set at spawn from
    /// the DungeonKind (the same theme colour as the boss aura).
    /// </summary>
    public class Building_VaultSigil : Building
    {
        private Color tint = Color.white;
        private Graphic cachedGraphic;

        public void SetTint(Color c) { tint = c; cachedGraphic = null; }

        private Graphic TintedGraphic
        {
            get
            {
                if (cachedGraphic == null && !def.graphicData.texPath.NullOrEmpty())
                    cachedGraphic = GraphicDatabase.Get<Graphic_Single>(
                        def.graphicData.texPath, ShaderDatabase.Transparent, def.graphicData.drawSize, tint);
                return cachedGraphic;
            }
        }

        public override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            var g = TintedGraphic;
            if (g != null) g.Draw(drawLoc, Rot4.North, this);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref tint, "tint", Color.white);
        }
    }
}
