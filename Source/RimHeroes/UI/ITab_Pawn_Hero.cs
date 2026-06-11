using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimHeroes
{
    /// <summary>
    /// Read-only class sheet: class, level, XP bar, granted features/abilities, upcoming unlocks.
    /// Visible only on Hero pawns. (VPE-style full editor UI comes later with spells/points.)
    /// </summary>
    public class ITab_Pawn_Hero : ITab
    {
        private Vector2 scroll;

        public ITab_Pawn_Hero()
        {
            size = new Vector2(440f, 450f);
            labelKey = "RH_TabHero";
        }

        public override bool IsVisible => SelPawn != null && HeroUtility.IsHero(SelPawn);

        public override void FillTab()
        {
            var hediff = HeroUtility.GetHeroHediff(SelPawn);
            if (hediff?.classDef == null)
            {
                return;
            }
            var classDef = hediff.classDef;
            var outRect = new Rect(0f, 0f, size.x, size.y).ContractedBy(12f);
            float y = outRect.y;

            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(outRect.x, y, outRect.width, 32f),
                "RH_TabHeader".Translate(classDef.LabelCap, hediff.level));
            y += 36f;
            Text.Font = GameFont.Small;

            // XP bar
            var barRect = new Rect(outRect.x, y, outRect.width, 24f);
            if (hediff.AtMaxLevel)
            {
                Widgets.FillableBar(barRect, 1f);
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(barRect, "RH_MaxLevel".Translate());
            }
            else
            {
                Widgets.FillableBar(barRect, Mathf.Clamp01(hediff.xp / hediff.XPForNextLevel));
                Text.Anchor = TextAnchor.MiddleCenter;
                Widgets.Label(barRect, $"{hediff.xp:F0} / {hediff.XPForNextLevel:F0} XP");
            }
            Text.Anchor = TextAnchor.UpperLeft;
            y += 32f;

            // Scrollable detail: current grants, then upcoming
            var listRect = new Rect(outRect.x, y, outRect.width, outRect.yMax - y);
            var sb = new StringBuilder();
            sb.AppendLine(classDef.description);
            sb.AppendLine();

            var current = classDef.levelGrants?.Where(g => g.level <= hediff.level).ToList();
            if (!current.NullOrEmpty())
            {
                sb.AppendLine("RH_TabFeatures".Translate().Resolve() + ":");
                foreach (var grant in current)
                {
                    foreach (var f in grant.features ?? Enumerable.Empty<HediffDef>())
                        sb.AppendLine($"  L{grant.level}: {f.LabelCap}");
                    foreach (var a in grant.abilities ?? Enumerable.Empty<AbilityDef>())
                        sb.AppendLine($"  L{grant.level}: {a.LabelCap}");
                }
                sb.AppendLine();
            }

            var upcoming = classDef.levelGrants?.Where(g => g.level > hediff.level).OrderBy(g => g.level).ToList();
            var gestrals = classDef.gestralUnlocks?.Where(g => g.level > hediff.level).OrderBy(g => g.level).ToList();
            if (!upcoming.NullOrEmpty() || !gestrals.NullOrEmpty())
            {
                sb.AppendLine("RH_TabUpcoming".Translate().Resolve() + ":");
                foreach (var grant in upcoming ?? Enumerable.Empty<HeroLevelGrant>())
                {
                    foreach (var f in grant.features ?? Enumerable.Empty<HediffDef>())
                        sb.AppendLine($"  L{grant.level}: {f.LabelCap}");
                    foreach (var a in grant.abilities ?? Enumerable.Empty<AbilityDef>())
                        sb.AppendLine($"  L{grant.level}: {a.LabelCap}");
                }
                foreach (var g in gestrals ?? Enumerable.Empty<GestralUnlock>())
                {
                    sb.AppendLine($"  L{g.level}: {"RH_TabGestral".Translate(g.job.LabelCap).Resolve()}");
                }
            }

            // Unlocked gestrals
            var unlockedGestrals = classDef.gestralUnlocks?.Where(g => g.level <= hediff.level).ToList();
            if (!unlockedGestrals.NullOrEmpty())
            {
                sb.AppendLine();
                sb.AppendLine("RH_TabGestralsUnlocked".Translate().Resolve() + ":");
                foreach (var g in unlockedGestrals)
                    sb.AppendLine($"  {g.job.LabelCap}");
            }

            string text = sb.ToString();
            float textHeight = Text.CalcHeight(text, listRect.width - 16f);
            Widgets.BeginScrollView(listRect, ref scroll, new Rect(0f, 0f, listRect.width - 16f, textHeight));
            Widgets.Label(new Rect(0f, 0f, listRect.width - 16f, textHeight), text);
            Widgets.EndScrollView();
        }
    }

    [StaticConstructorOnStartup]
    public static class HeroITabInjector
    {
        static HeroITabInjector()
        {
            foreach (var def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (def.race?.Humanlike != true)
                {
                    continue;
                }
                if (def.inspectorTabs == null)
                {
                    def.inspectorTabs = new System.Collections.Generic.List<System.Type>();
                }
                if (def.inspectorTabsResolved == null)
                {
                    def.inspectorTabsResolved = new System.Collections.Generic.List<InspectTabBase>();
                }
                if (!def.inspectorTabs.Contains(typeof(ITab_Pawn_Hero)))
                {
                    def.inspectorTabs.Add(typeof(ITab_Pawn_Hero));
                    def.inspectorTabsResolved.Add(InspectTabManager.GetSharedInstance(typeof(ITab_Pawn_Hero)));
                }
            }
        }
    }
}
