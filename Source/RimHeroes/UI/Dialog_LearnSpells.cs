using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Known casters (Sorcerer/Bard/Ranger/Warlock) learn a capped set of spells. Shown at hero
    /// creation and whenever new spells/levels unlock: the player picks cantrips and leveled spells
    /// from the class pool up to the allowed counts. Picks are permanent (5e: spells known).
    /// </summary>
    public class Dialog_LearnSpells : Window
    {
        private readonly Hediff_HeroLevels hero;
        private readonly Action onDone;
        private Vector2 scroll;

        public Dialog_LearnSpells(Hediff_HeroLevels hero, Action onDone)
        {
            this.hero = hero;
            this.onDone = onDone;
            forcePause = true;
            closeOnClickedOutside = false;
            closeOnAccept = false;
            closeOnCancel = false;
            doCloseX = false;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(560f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), "Learn spells");
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.8f, 0.8f, 0.8f);
            Widgets.Label(new Rect(0f, 34f, inRect.width, 22f),
                $"{hero.pawn.LabelShortCap} commits new magic to memory. Choose what to learn.");
            GUI.color = Color.white;

            var pool = hero.LearnableSpellPool().Distinct().ToList();
            var cantrips = pool.Where(s => s.level == 0).ToList();
            var leveled = pool.Where(s => s.level > 0).OrderBy(s => s.level).ToList();

            var outRect = new Rect(0f, 62f, inRect.width, inRect.height - 62f - 44f);
            float viewH = 30f + cantrips.Count * 60f + 30f + leveled.Count * 60f + 20f;
            var viewRect = new Rect(0f, 0f, inRect.width - 20f, viewH);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            y = Section(viewRect.width, y, $"Cantrips ({hero.KnownCantrips}/{hero.CantripsAllowed})", cantrips,
                hero.KnownCantrips < hero.CantripsAllowed);
            y = Section(viewRect.width, y, $"Spells ({hero.KnownLeveledSpells}/{hero.LeveledSpellsAllowed})", leveled,
                hero.KnownLeveledSpells < hero.LeveledSpellsAllowed);
            Widgets.EndScrollView();

            bool done = !HeroChoices.OwesSpells(hero);
            var btn = new Rect(inRect.width - 160f, inRect.height - 38f, 160f, 34f);
            if (Widgets.ButtonText(btn, done ? "Done" : "Learn the rest later"))
            {
                Close();
                onDone?.Invoke();
            }
        }

        private float Section(float width, float y, string header, List<AbilityDef> spells, bool canLearnMore)
        {
            Text.Font = GameFont.Small;
            GUI.color = new Color(0.95f, 0.92f, 0.7f);
            Widgets.Label(new Rect(0f, y, width, 26f), header);
            GUI.color = Color.white;
            y += 30f;
            foreach (var def in spells)
            {
                var card = new Rect(0f, y, width, 56f);
                Widgets.DrawMenuSection(card);
                var inner = card.ContractedBy(8f);
                bool known = hero.HasLearned(def);
                if (def.uiIcon != null) GUI.DrawTexture(new Rect(inner.x, inner.y + 2f, 36f, 36f), def.uiIcon);
                float tx = inner.x + 44f;
                GUI.color = known ? new Color(0.6f, 0.85f, 0.6f) : new Color(0.95f, 0.92f, 0.7f);
                Widgets.Label(new Rect(tx, inner.y, inner.width - 150f, 22f), def.LabelCap);
                GUI.color = new Color(0.78f, 0.78f, 0.78f);
                Text.Font = GameFont.Tiny;
                Widgets.Label(new Rect(tx, inner.y + 20f, inner.width - 150f, inner.height - 20f), def.description ?? "");
                Text.Font = GameFont.Small;
                GUI.color = Color.white;
                var bRect = new Rect(card.xMax - 96f, card.y + (56f - 30f) / 2f, 84f, 30f);
                if (known)
                {
                    GUI.color = new Color(0.6f, 0.85f, 0.6f);
                    Widgets.Label(new Rect(bRect.x + 14f, bRect.y + 5f, 80f, 24f), "Known");
                    GUI.color = Color.white;
                }
                else if (canLearnMore && Widgets.ButtonText(bRect, "Learn"))
                {
                    hero.LearnSpell(def);
                }
                y += 60f;
            }
            return y;
        }
    }
}
