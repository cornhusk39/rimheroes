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

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), title);
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(0f, 34f, inRect.width, 24f), subtitle);
            GUI.color = Color.white;

            const float rowH = 86f;
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, options.Count * (rowH + 8f));
            var outRect = new Rect(0f, 64f, inRect.width, inRect.height - 64f);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            foreach (var opt in options)
            {
                var card = new Rect(0f, y, viewRect.width, rowH);
                Widgets.DrawMenuSection(card);
                var inner = card.ContractedBy(10f);
                if (opt.icon != null)
                {
                    GUI.DrawTexture(new Rect(inner.x, inner.y + 4f, 40f, 40f), opt.icon);
                }
                float tx = opt.icon != null ? inner.x + 48f : inner.x;
                Text.Font = GameFont.Small;
                GUI.color = new Color(0.95f, 0.92f, 0.7f);
                Widgets.Label(new Rect(tx, inner.y, inner.width - 110f, 24f), opt.label);
                GUI.color = new Color(0.82f, 0.82f, 0.82f);
                Widgets.Label(new Rect(tx, inner.y + 24f, inner.width - 110f, inner.height - 24f), opt.description ?? "");
                GUI.color = Color.white;
                if (Widgets.ButtonText(new Rect(card.xMax - 96f, card.y + (rowH - 32f) / 2f, 84f, 32f), "Pick"))
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
