using System;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>The long-rest toggle, with a fill bar across the bottom showing how far the sleep has
    /// progressed (like vanilla deathrest). The bar only draws while a rest is underway.</summary>
    [StaticConstructorOnStartup]
    public class Command_LongRest : Command_Toggle
    {
        public Func<float> progress;

        private static readonly Texture2D FillTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.32f, 0.55f, 0.85f));

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            GizmoResult result = base.GizmoOnGUI(topLeft, maxWidth, parms);
            float p = progress != null ? progress() : 0f;
            if (p > 0f)
            {
                Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
                Rect bar = new Rect(rect.x + 4f, rect.yMax - 9f, rect.width - 8f, 6f);
                Widgets.FillableBar(bar, Mathf.Clamp01(p), FillTex, BaseContent.BlackTex, false);
            }
            return result;
        }
    }
}
