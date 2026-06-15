using System;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace RimHeroes
{
    /// <summary>
    /// Animated d20 roll. The result is decided up front (in D20.Roll); this window is pure theater:
    /// the number ticks and decelerates, lands on the rolled value, flashes gold on a natural 20 or
    /// red on a natural 1, then shows the outcome and a Continue button that fires the callback.
    /// Animation is driven off real elapsed time (not deltaTime, which IMGUI double-counts).
    /// </summary>
    public class Dialog_D20Roll : Window
    {
        private readonly Pawn pawn;
        private readonly CheckDef check;
        private readonly RollResult result;
        private readonly Action<RollResult> onResolved;
        private readonly string title;

        private float startTime = -1f;
        private float nextSwapAt;
        private int displayNum = 1;
        private bool landed;
        private bool resolved;

        private const float SpinDuration = 1.5f;
        private const float DieSize = 156f;

        private static readonly Color Gold = new Color(0.92f, 0.82f, 0.45f);
        private static readonly Color Muted = new Color(0.78f, 0.78f, 0.78f);
        private static readonly Color CritGold = new Color(1f, 0.86f, 0.32f);
        private static readonly Color CritRed = new Color(0.95f, 0.36f, 0.30f);
        private static readonly Color GoodGreen = new Color(0.60f, 0.90f, 0.60f);

        public override Vector2 InitialSize => new Vector2(460f, 440f);

        public Dialog_D20Roll(Pawn pawn, CheckDef check, RollResult result, Action<RollResult> onResolved)
        {
            this.pawn = pawn;
            this.check = check;
            this.result = result;
            this.onResolved = onResolved;
            title = check?.LabelCap ?? "Skill check";
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;
            closeOnCancel = false;
            closeOnAccept = false;
            doCloseX = false;
            preventCameraMotion = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            float now = Time.realtimeSinceStartup;
            if (startTime < 0f) { startTime = now; nextSwapAt = now; }
            float t = now - startTime;

            // header
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperCenter;
            GUI.color = Gold;
            Widgets.Label(new Rect(0f, 4f, inRect.width, 34f), title);
            Text.Font = GameFont.Small;
            GUI.color = Muted;
            string modStr = (result.modifier >= 0 ? "+" : "") + result.modifier;
            string who = pawn != null ? pawn.LabelShortCap + "   " : "";
            Widgets.Label(new Rect(0f, 38f, inRect.width, 22f), $"{who}d20 {modStr}   vs   DC {result.dc}");
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            // die + number
            var dieRect = new Rect((inRect.width - DieSize) / 2f, 70f, DieSize, DieSize);
            if (RH_Tex.D20Die != null) GUI.DrawTexture(dieRect, RH_Tex.D20Die);

            if (t < SpinDuration)
            {
                if (now >= nextSwapAt)
                {
                    displayNum = UnityEngine.Random.Range(1, 21);   // cosmetic only, never the game RNG
                    float frac = Mathf.Clamp01(t / SpinDuration);
                    nextSwapAt = now + Mathf.Lerp(0.035f, 0.17f, frac);  // ease-out: slows toward the landing
                    SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
                }
            }
            else
            {
                displayNum = result.rawDie;
                if (!landed)
                {
                    landed = true;
                    SoundDefOf.Click.PlayOneShotOnCamera();
                    if (result.bucket == RollBucket.CriticalSuccess || result.bucket == RollBucket.CriticalFailure)
                        SoundDefOf.Tick_High.PlayOneShotOnCamera();
                }
            }

            Color numColor = Color.white;
            if (landed && result.bucket == RollBucket.CriticalSuccess) numColor = CritGold;
            else if (landed && result.bucket == RollBucket.CriticalFailure) numColor = CritRed;
            DrawBigNumber(new Rect(dieRect.x, dieRect.y - 8f, dieRect.width, dieRect.height), displayNum.ToString(), numColor);

            if (!landed) return;

            // result block
            float y = dieRect.yMax + 6f;
            Text.Anchor = TextAnchor.UpperCenter;
            Text.Font = GameFont.Small;
            GUI.color = Muted;
            Widgets.Label(new Rect(0f, y, inRect.width, 22f), $"{result.rawDie} {modStr} = {result.total}   vs   DC {result.dc}");
            y += 26f;

            Text.Font = GameFont.Medium;
            GUI.color = OutcomeColor(result.bucket);
            Widgets.Label(new Rect(0f, y, inRect.width, 30f), OutcomeTitle(result.bucket));
            y += 34f;

            string flavor = OutcomeFlavor();
            if (!flavor.NullOrEmpty())
            {
                Text.Font = GameFont.Small;
                GUI.color = Muted;
                Widgets.Label(new Rect(24f, y, inRect.width - 48f, inRect.height - y - 50f), flavor);
            }
            GUI.color = Color.white;
            Text.Anchor = TextAnchor.UpperLeft;

            var btn = new Rect((inRect.width - 160f) / 2f, inRect.height - 42f, 160f, 34f);
            if (Widgets.ButtonText(btn, "Continue"))
            {
                Finish();
            }
        }

        private void Finish()
        {
            if (resolved) return;
            resolved = true;
            onResolved?.Invoke(result);
            Close();
        }

        // Scales the GUI matrix around the die center to draw an oversized number with the vanilla font.
        private static void DrawBigNumber(Rect rect, string s, Color color)
        {
            Matrix4x4 prevMatrix = GUI.matrix;
            GameFont prevFont = Text.Font;
            TextAnchor prevAnchor = Text.Anchor;
            GUIUtility.ScaleAroundPivot(new Vector2(2.9f, 2.9f), rect.center);
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.MiddleCenter;
            GUI.color = color;
            Widgets.Label(rect, s);
            GUI.color = Color.white;
            Text.Font = prevFont;
            Text.Anchor = prevAnchor;
            GUI.matrix = prevMatrix;
        }

        private static string OutcomeTitle(RollBucket b)
        {
            switch (b)
            {
                case RollBucket.CriticalSuccess: return "Critical success!";
                case RollBucket.Success: return "Success";
                case RollBucket.Failure: return "Failure";
                default: return "Critical failure!";
            }
        }

        private static Color OutcomeColor(RollBucket b)
        {
            switch (b)
            {
                case RollBucket.CriticalSuccess: return CritGold;
                case RollBucket.Success: return GoodGreen;
                case RollBucket.Failure: return Muted;
                default: return CritRed;
            }
        }

        private string OutcomeFlavor()
        {
            if (check == null) return null;
            switch (result.bucket)
            {
                case RollBucket.CriticalSuccess: return check.critSuccessText ?? check.successText;
                case RollBucket.Success: return check.successText;
                case RollBucket.Failure: return check.failureText;
                default: return check.critFailureText ?? check.failureText;
            }
        }
    }
}
