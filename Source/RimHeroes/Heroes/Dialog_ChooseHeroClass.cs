using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimHeroes
{
    /// <summary>
    /// Light class-picker shown at game start (and later from Class Tomes). Cannot be dismissed
    /// without choosing - a Hero without a class is not a thing.
    /// </summary>
    public class Dialog_ChooseHeroClass : Window
    {
        private readonly Pawn pawn;
        private HeroClassDef selected;
        private Vector2 scroll;

        private const float RowHeight = 92f;
        private const float RowGap = 8f;

        private static List<HeroClassDef> Classes => DefDatabase<HeroClassDef>.AllDefsListForReading;

        public override Vector2 InitialSize => new Vector2(640f, 540f);

        public Dialog_ChooseHeroClass(Pawn pawn)
        {
            this.pawn = pawn;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnCancel = false;
            closeOnAccept = false;
            doCloseX = false;
        }

        private static readonly Color Gold = new Color(0.92f, 0.82f, 0.45f);

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            GUI.color = Gold;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 36f), "RH_ChooseClassTitle".Translate(pawn.LabelShortCap));
            GUI.color = Color.white;
            Text.Font = GameFont.Small;
            Widgets.DrawLineHorizontal(0f, 40f, inRect.width);

            var listRect = new Rect(0f, 48f, inRect.width, inRect.height - 48f - 50f);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, Classes.Count * (RowHeight + RowGap));
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            float y = 0f;
            foreach (var classDef in Classes)
            {
                var row = new Rect(0f, y, viewRect.width, RowHeight);
                Widgets.DrawOptionBackground(row, selected == classDef);
                if (selected != classDef && Mouse.IsOver(row)) Widgets.DrawHighlight(row);
                if (Widgets.ButtonInvisible(row))
                {
                    selected = classDef;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                var inner = row.ContractedBy(10f);
                Text.Font = GameFont.Medium;
                GUI.color = Gold;
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, 30f), classDef.LabelCap);
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Text.Font = GameFont.Small;
                Widgets.Label(new Rect(inner.x, inner.y + 30f, inner.width, inner.height - 30f), classDef.description);
                GUI.color = Color.white;
                y += RowHeight + RowGap;
            }
            Widgets.EndScrollView();

            var buttonRect = new Rect(inRect.width - 200f, inRect.height - 44f, 200f, 40f);
            if (Widgets.ButtonText(buttonRect, "RH_ConfirmClass".Translate(), drawBackground: true, doMouseoverSound: true, active: selected != null)
                && selected != null)
            {
                Confirm(selected);
            }
        }

        public void Confirm(HeroClassDef classDef)
        {
            var hero = HeroUtility.MakeHero(pawn, classDef);
            HeroUtility.GrantStarterWeapon(pawn, classDef);
            Messages.Message("RH_BecameHero".Translate(pawn.LabelShortCap, classDef.label), pawn, MessageTypeDefOf.PositiveEvent);
            Close();
            // Any level-1 picks (e.g. the Fighter's Fighting Style) open right after the class is set.
            if (hero != null) HeroChoices.CheckLevelChoices(hero);
        }
    }
}
