using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Pick one known spell of a given spell level (used by Spell Mastery and Signature Spells).
    /// Lists the hero's known spells of that level; selecting one invokes onPick and closes.
    /// </summary>
    public class Dialog_PickSpell : Window
    {
        private readonly string title;
        private readonly AbilityDef current;
        private readonly Action<AbilityDef> onPick;
        private readonly List<AbilityDef> options;
        private Vector2 scroll;

        public Dialog_PickSpell(Hediff_HeroLevels hero, int spellLevel, string title,
            AbilityDef current, AbilityDef exclude, Action<AbilityDef> onPick)
        {
            this.title = title;
            this.current = current;
            this.onPick = onPick;
            options = hero.KnownSpellsOfLevel(spellLevel).Where(d => d != exclude).ToList();
            forcePause = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(520f, 480f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), title);
            Text.Font = GameFont.Small;

            if (options.Count == 0)
            {
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                Widgets.Label(new Rect(0f, 40f, inRect.width, 40f), "No known spells of this level yet.");
                GUI.color = Color.white;
                return;
            }

            const float rowH = 64f;
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, options.Count * (rowH + 8f));
            var outRect = new Rect(0f, 44f, inRect.width, inRect.height - 44f);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            foreach (var def in options)
            {
                var card = new Rect(0f, y, viewRect.width, rowH);
                Widgets.DrawMenuSection(card);
                if (def == current)
                {
                    Widgets.DrawHighlightSelected(card);
                }
                var inner = card.ContractedBy(8f);
                if (def.uiIcon != null)
                {
                    GUI.DrawTexture(new Rect(inner.x, inner.y + 4f, 40f, 40f), def.uiIcon);
                }
                float tx = inner.x + 48f;
                GUI.color = new Color(0.95f, 0.92f, 0.7f);
                Widgets.Label(new Rect(tx, inner.y, inner.width - 150f, 24f), def.LabelCap);
                GUI.color = new Color(0.8f, 0.8f, 0.8f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(tx, inner.y + 22f, inner.width - 150f, inner.height - 22f), def.description ?? "");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                if (Widgets.ButtonText(new Rect(card.xMax - 92f, card.y + (rowH - 30f) / 2f, 80f, 30f), "Choose"))
                {
                    onPick?.Invoke(def);
                    Close();
                    return;
                }
                y += rowH + 8f;
            }
            Widgets.EndScrollView();
        }
    }
}
