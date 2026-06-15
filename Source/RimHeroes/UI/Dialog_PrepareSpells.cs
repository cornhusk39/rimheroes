using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Prepared-caster spell management. Lists the hero's known leveled spells with a ready/unready
    /// toggle, capped at PreparedMax. Editable only while the 6h post-long-rest window is open.
    /// </summary>
    public class Dialog_PrepareSpells : Window
    {
        private readonly Hediff_HeroLevels hero;
        private Vector2 scroll;

        public Dialog_PrepareSpells(Hediff_HeroLevels hero)
        {
            this.hero = hero;
            forcePause = true;
            doCloseX = true;
            doCloseButton = true;
            absorbInputAroundWindow = true;
        }

        public override Vector2 InitialSize => new Vector2(520f, 600f);

        public override void DoWindowContents(Rect inRect)
        {
            var pawn = hero.pawn;
            bool editable = hero.PrepareWindowOpen;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 34f), "Prepare spells: " + pawn.LabelShortCap);
            Text.Font = GameFont.Small;
            GUI.color = editable ? new Color(0.7f, 0.9f, 0.7f) : new Color(0.85f, 0.7f, 0.5f);
            Widgets.Label(new Rect(0f, 36f, inRect.width, 24f),
                $"Prepared {hero.PreparedLeveledCount} of {hero.PreparedMax}" +
                (editable ? "   (you may change these now)" : "   (locked. Finish a long rest to change them.)"));
            GUI.color = Color.white;

            var spells = pawn.abilities.abilities
                .Where(a => a is Ability_Spell && a.def.level > 0)
                .Select(a => a.def).Distinct()
                .OrderBy(d => d.level).ThenBy(d => d.label).ToList();

            const float rowH = 38f;
            var viewRect = new Rect(0f, 0f, inRect.width - 24f, spells.Count * rowH + 4f);
            var outRect = new Rect(0f, 64f, inRect.width, inRect.height - 64f - 44f);
            Widgets.BeginScrollView(outRect, ref scroll, viewRect);
            float y = 0f;
            foreach (var def in spells)
            {
                var row = new Rect(0f, y, viewRect.width, rowH - 2f);
                if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                bool prepared = hero.IsPrepared(def);
                if (def.uiIcon != null)
                {
                    GUI.DrawTexture(new Rect(row.x + 4f, row.y + 4f, 28f, 28f), def.uiIcon);
                }
                Widgets.Label(new Rect(row.x + 40f, row.y + 8f, row.width - 150f, 24f),
                    $"{def.LabelCap}  (lvl {def.level})");
                var cb = new Rect(row.xMax - 104f, row.y + 6f, 96f, 26f);
                if (editable)
                {
                    bool now = prepared;
                    Widgets.CheckboxLabeled(cb, "ready", ref now);
                    if (now && !prepared)
                    {
                        if (!hero.SetPrepared(def, true))
                            Messages.Message("Your prepared spells are full. Unready one first.",
                                MessageTypeDefOf.RejectInput, false);
                    }
                    else if (!now && prepared)
                    {
                        hero.SetPrepared(def, false);
                    }
                }
                else
                {
                    GUI.color = prepared ? new Color(0.7f, 0.9f, 0.7f) : new Color(0.6f, 0.6f, 0.6f);
                    Widgets.Label(cb, prepared ? "ready" : "not ready");
                    GUI.color = Color.white;
                }
                y += rowH;
            }
            Widgets.EndScrollView();
        }
    }
}
