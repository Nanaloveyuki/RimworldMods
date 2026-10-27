#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using IrisMenus.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal sealed class MainButtonsPage
    {
        private sealed class ButtonRow
        {
            internal readonly string DefName, Label, Tooltip;
            internal readonly UiCheckbox Visibility;
            internal readonly UiTooltip Tip;
            internal float Y, Height, LabelHeight;
            internal Vector2 TipSize;

            internal ButtonRow(UiContext ui, string defName, string label, string tooltip)
            {
                DefName = defName;
                Label = label;
                Tooltip = tooltip;
                Visibility = new UiCheckbox(ui);
                Tip = new UiTooltip(ui);
            }
        }

        private readonly UiContext ui;
        private readonly List<ButtonRow> rows = new List<ButtonRow>();
        private readonly UiScrollView scroll;
        private readonly UiButton restore;
        private readonly Action<Rect> drawContent;
        private readonly string title = "IM_MainButtonsTitle".Translate();
        private readonly string explanation = "IM_MainButtonsHint".Translate();
        private readonly string restoreLabel = "IM_MainButtonsRestoreAll".Translate();
        private readonly string show = "IM_MainButtonsShow".Translate();
        private float width = -1f, scale = -1f, density = -1f, buttonWidth;
        private float contentHeight, titleHeight, hintHeight, actionY, actionHeight, rowsY, textWidth, padding, actionWidth;
        private bool compact;

        internal MainButtonsPage(UiContext ui)
        {
            this.ui = ui;
            scroll = new UiScrollView(ui);
            restore = new UiButton(ui);
            drawContent = DrawContent;
            var defs = new List<MainButtonDef>(DefDatabase<MainButtonDef>.AllDefsListForReading);
            defs.Sort((left, right) =>
            {
                int order = left.order.CompareTo(right.order);
                return order != 0 ? order : string.Compare(left.defName, right.defName, StringComparison.Ordinal);
            });
            var known = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < defs.Count; i++)
            {
                MainButtonDef def = defs[i];
                known.Add(def.defName);
                string label = def.LabelCap;
                rows.Add(new ButtonRow(ui, def.defName, label, string.IsNullOrEmpty(def.description) ? label : label + "\n" + def.description));
            }
            List<string> hidden = MainButtonVisibility.Hidden;
            for (int i = 0; i < hidden.Count; i++)
            {
                string name = hidden[i];
                if (string.IsNullOrEmpty(name) || !known.Add(name)) continue;
                string label = "IM_MainButtonsMissing".Translate(name);
                rows.Add(new ButtonRow(ui, name, label, label + "\n" + "IM_MainButtonsMissingHint".Translate()));
            }
        }

        internal void Draw(Rect rect, float unit, float rowHeight)
        {
            float oldSize = ui.FontSize;
            FontStyle oldStyle = ui.FontStyle;
            try
            {
                Layout(Mathf.Max(1f, rect.width - 20f), unit, rowHeight);
                scroll.Draw(rect, contentHeight, drawContent);
            }
            finally { ui.FontSize = oldSize; ui.FontStyle = oldStyle; }
        }

        private float Measure(string text, float available, float font, FontStyle style = FontStyle.Normal)
        {
            ui.FontSize = font * scale;
            ui.FontStyle = style;
            return Mathf.Ceil(UiTextMetrics.Height(ui, text, Mathf.Max(1f, available))) + 2f * scale;
        }

        private void Layout(float available, float unit, float rowHeight)
        {
            if (width == available && scale == unit && density == rowHeight) return;
            width = available; scale = unit; density = rowHeight;
            compact = width < 500f * scale;
            padding = Mathf.Min(16f * scale, width * 0.04f);
            actionWidth = Mathf.Min(140f * scale, width * 0.3f);
            textWidth = Mathf.Max(1f, width - padding * 2f - (compact ? 0f : actionWidth + 20f * scale));
            titleHeight = Measure(title, width, 22f, FontStyle.Bold);
            hintHeight = Measure(explanation, width, 13f);
            actionY = titleHeight + 10f * scale + hintHeight + 20f * scale;
            buttonWidth = Mathf.Min(width, 280f * scale);
            actionHeight = Mathf.Max(40f * scale, Measure(restoreLabel, buttonWidth - 80f * scale, 13f) + 12f * scale);
            rowsY = actionY + actionHeight + 24f * scale;
            float tipWidth = Mathf.Min(420f * scale, width);
            float y = rowsY;
            for (int i = 0; i < rows.Count; i++)
            {
                ButtonRow row = rows[i];
                row.Y = y;
                row.LabelHeight = Measure(row.Label, textWidth, 15f, FontStyle.Bold);
                row.Height = Mathf.Max(rowHeight + 24f * scale,
                    row.LabelHeight + padding * 2f + (compact ? 40f * scale : 0f));
                row.TipSize = new Vector2(tipWidth, Measure(row.Tooltip, tipWidth - 20f * scale, 12f) + 14f * scale);
                y += row.Height + 8f * scale;
            }
            contentHeight = y;
        }

        private void DrawContent(Rect rect)
        {
            UiPalette palette = ui.Palette;
            ui.FontSize = 22f * scale; ui.FontStyle = FontStyle.Bold;
            ui.Label(new Rect(0f, 0f, width, titleHeight), title, palette.Ink, TextAnchor.UpperLeft, true);
            ui.FontSize = 13f * scale; ui.FontStyle = FontStyle.Normal;
            ui.Label(new Rect(0f, titleHeight + 10f * scale, width, hintHeight), explanation, palette.Muted, TextAnchor.UpperLeft, true);
            Rect restoreRect = new Rect(0f, actionY, buttonWidth, actionHeight);
            if (DrawAction(restore, restoreRect, restoreLabel, MainButtonVisibility.Hidden.Count > 0, false))
                MainButtonVisibility.RestoreAll();
            for (int i = 0; i < rows.Count; i++)
            {
                ButtonRow row = rows[i];
                Rect bounds = new Rect(0f, row.Y, width, row.Height);
                if (!bounds.Overlaps(scroll.VisibleRect)) continue;
                ui.Fill(bounds, palette.Raised);
                ui.Fill(new Rect(0f, bounds.yMax - 1f, width, 1f), palette.Line);
                ui.FontSize = 15f * scale; ui.FontStyle = FontStyle.Bold;
                ui.Label(new Rect(padding, row.Y + padding, textWidth, row.LabelHeight), row.Label, palette.Ink, TextAnchor.UpperLeft, true);
                ui.FontStyle = FontStyle.Normal;
                Rect action = compact
                    ? new Rect(padding, bounds.yMax - padding - 32f * scale, width - padding * 2f, 32f * scale)
                    : new Rect(bounds.xMax - padding - actionWidth, bounds.center.y - 18f * scale, actionWidth, 36f * scale);
                ui.FontSize = 12f * scale;
                bool visible = !MainButtonVisibility.IsHidden(row.DefName);
                bool next = row.Visibility.Draw(action, visible, show, scale: scale);
                if (next != visible) MainButtonVisibility.SetHidden(row.DefName, !next);
                row.Tip.Delay = IrisMenusMod.Instance.Settings.TipDelay;
                row.Tip.Observe(bounds, row.Tooltip, row.Visibility.Id, IrisMenusMod.Instance.Settings.ShowTips);
            }
        }

        private bool DrawAction(UiButton button, Rect rect, string label, bool enabled, bool primary)
        {
            UiInteraction interaction = ui.Interact(button.Id, rect, enabled);
            Color ink = button.DrawVisual(rect, label, interaction, enabled, primary, scale: scale, drawContent: false);
            ui.Label(new Rect(rect.x + 40f * scale, rect.y, Mathf.Max(1f, rect.width - 80f * scale), rect.height),
                label, ink, TextAnchor.MiddleCenter, true);
            return interaction.Clicked;
        }

        internal void DrawOverlay(Rect viewport)
        {
            float oldSize = ui.FontSize;
            ui.FontSize = 12f * scale;
            for (int i = 0; i < rows.Count; i++) rows[i].Tip.DrawOverlay(viewport, rows[i].TipSize, scale);
            ui.FontSize = oldSize;
        }
    }
}
#endif
