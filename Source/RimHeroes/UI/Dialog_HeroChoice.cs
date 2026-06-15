using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>Generic "pick one of N" hero level-up dialog (used for trait picks and feat picks).</summary>
    public class Dialog_HeroChoice : Window
    {
        private readonly string title;
        private readonly string subtitle;
        private readonly List<HeroChoiceOption> options;
        private readonly Action onDone;
        private Vector2 scroll;

        public Dialog_HeroChoice(string title, string subtitle, List<HeroChoiceOption> options, Action onDone)
        {
            this.title = title;
            this.subtitle = subtitle;
            this.options = options;
            this.onDone = onDone;
            forcePause = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            doCloseX = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(560f, 540f);

        private static readonly Color Gold = new Color(0.92f, 0.82f, 0.45f);
        private static readonly Color Muted = new Color(0.78f, 0.78f, 0.78f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            GUI.color = Gold;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), title);
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            GUI.color = Muted;
            Widgets.Label(new Rect(0f, 34f, inRect.width, 24f), subtitle);
            GUI.color = Color.white;
            Widgets.DrawLineHorizontal(0f, 60f, inRect.width);

            const float rowH = 88f;
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, options.Count * (rowH + 8f));
            var outRect = new Rect(0f, 68f, inRect.width, inRect.height - 68f);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            foreach (var opt in options)
            {
                var card = new Rect(0f, y, viewRect.width, rowH);
                Widgets.DrawMenuSection(card);
                bool hovered = Mouse.IsOver(card);
                if (hovered) Widgets.DrawHighlight(card);
                var inner = card.ContractedBy(12f);
                if (opt.icon != null)
                {
                    GUI.DrawTexture(new Rect(inner.x, inner.y + 4f, 42f, 42f), opt.icon);
                }
                float tx = opt.icon != null ? inner.x + 52f : inner.x;
                Text.Font = GameFont.Small;
                GUI.color = Gold;
                Widgets.Label(new Rect(tx, inner.y, inner.width - 16f, 24f), opt.label);
                GUI.color = Muted;
                Widgets.Label(new Rect(tx, inner.y + 24f, inner.width - 16f, inner.height - 24f), opt.description ?? "");
                GUI.color = Color.white;
                // The whole card is clickable; a hint chevron sits on the right.
                Text.Anchor = TextAnchor.MiddleRight;
                GUI.color = hovered ? Gold : Muted;
                Widgets.Label(new Rect(card.xMax - 40f, card.y, 28f, rowH), "›");
                GUI.color = Color.white;
                Text.Anchor = TextAnchor.UpperLeft;
                if (Widgets.ButtonInvisible(card))
                {
                    opt.apply?.Invoke();
                    onDone?.Invoke();
                    Close();
                    return;
                }
                y += rowH + 8f;
            }
            Widgets.EndScrollView();
        }
    }
}
