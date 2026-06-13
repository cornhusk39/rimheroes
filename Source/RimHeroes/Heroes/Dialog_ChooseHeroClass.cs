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

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(0f, 0f, inRect.width, 36f), "RH_ChooseClassTitle".Translate(pawn.LabelShortCap));
            Text.Font = GameFont.Small;

            var listRect = new Rect(0f, 44f, inRect.width, inRect.height - 44f - 50f);
            var viewRect = new Rect(0f, 0f, listRect.width - 16f, Classes.Count * (RowHeight + RowGap));
            Widgets.BeginScrollView(listRect, ref scroll, viewRect);
            float y = 0f;
            foreach (var classDef in Classes)
            {
                var row = new Rect(0f, y, viewRect.width, RowHeight);
                Widgets.DrawOptionBackground(row, selected == classDef);
                if (Widgets.ButtonInvisible(row))
                {
                    selected = classDef;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                }
                var inner = row.ContractedBy(8f);
                Text.Font = GameFont.Medium;
                Widgets.Label(new Rect(inner.x, inner.y, inner.width, 30f), classDef.LabelCap);
                Text.Font = GameFont.Small;
                GUI.color = new Color(0.85f, 0.85f, 0.85f);
                Widgets.Label(new Rect(inner.x, inner.y + 30f, inner.width, inner.height - 30f), classDef.description);
                GUI.color = Color.white;
                y += RowHeight + RowGap;
            }
            Widgets.EndScrollView();

            var buttonRect = new Rect(inRect.width - 190f, inRect.height - 42f, 190f, 38f);
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
