extern alias iris;
using System;
using System.Collections.Generic;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using iris::IrisMenus;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal static class RHAH_IrisMenusWidgets
    {
        internal const float CardPad = 4f;
        internal const float CardGap = 4f;
        internal static RHAH_IrisMenusNativePage Native { get; set; }

        internal static bool IsVisible(Rect rect)
        {
            return Native == null || (rect.Overlaps(Native.VisibleRect) && Native.Context.IsRectVisible(rect));
        }

        internal static void Label(Listing_Standard list, string label)
        {
            float height = Native == null ? Text.CalcHeight(label, list.ColumnWidth) : Native.LabelHeight(label, list.ColumnWidth);
            Rect row = list.GetRect(height);
            list.Gap(list.verticalSpacing);
            Label(row, label);
        }

        internal static void Label(Rect row, string label)
        {
            if (!IsVisible(row)) return;
            if (Native == null) Widgets.Label(row, label);
            else Native.Context.Label(row, label, Native.Context.Palette.Ink, TextAnchor.UpperLeft, true);
        }

        internal static void Section(Listing_Standard list, string title)
        {
            if (Native == null)
            {
                MenuControls.Section(list, title);
                return;
            }
            var context = Native.Context;
            float oldSize = context.FontSize;
            FontStyle oldStyle = context.FontStyle;
            float textHeight;
            try
            {
                context.FontSize = 12f * Native.Scale;
                context.FontStyle = FontStyle.Bold;
                textHeight = Native.LabelHeight(title, Mathf.Max(1f, list.ColumnWidth - 12f * Native.Scale));
            }
            finally { context.FontSize = oldSize; context.FontStyle = oldStyle; }
            float height = Mathf.Max(Native.SectionTitle.Measure(Native.Scale), textHeight + 16f * Native.Scale);
            Rect row = list.GetRect(height);
            list.Gap(list.verticalSpacing);
            if (!IsVisible(row)) return;
            if (textHeight <= 32f * Native.Scale) Native.SectionTitle.Draw(row, title, Native.Scale);
            else
            {
                Native.SectionTitle.Draw(row, string.Empty, Native.Scale);
                try
                {
                    context.FontSize = 12f * Native.Scale;
                    context.FontStyle = FontStyle.Bold;
                    context.Label(new Rect(row.x + 12f * Native.Scale, row.y + 8f * Native.Scale,
                        Mathf.Max(0f, row.width - 12f * Native.Scale), textHeight), title,
                        context.Palette.Muted, TextAnchor.UpperLeft, true);
                }
                finally { context.FontSize = oldSize; context.FontStyle = oldStyle; }
            }
        }

        internal static bool Checkbox(Listing_Standard list, string label, ref bool value, string tooltip = null)
        {
            return Checkbox(list, null, label, ref value, tooltip);
        }

        internal static bool Checkbox(Listing_Standard list, string anchor, string label, ref bool value, string tooltip = null)
        {
            string id = anchor ?? label;
            float height = Native == null ? Mathf.Max(24f, Text.CalcHeight(label, list.ColumnWidth)) : Native.SwitchHeight(id, label, list.ColumnWidth);
            if (anchor != null) MenuControls.Anchor(list, anchor, height);
            Rect row = list.GetRect(height);
            list.Gap(list.verticalSpacing);
            if (!IsVisible(row)) return false;
            value = Switch(row, id, label, value, tooltip);
            return true;
        }

        internal static bool Switch(Rect row, string stableId, string label, bool value, string tip = null)
        {
            if (!IsVisible(row)) return value;
            if (Native != null) return Native.Switch(row, stableId, label, value, tip);
            if (!string.IsNullOrEmpty(tip))
            {
                Widgets.DrawHighlightIfMouseover(row);
                TooltipHandler.TipRegion(row, tip);
            }
            Widgets.CheckboxLabeled(row, label, ref value);
            return value;
        }

        internal static bool Button(Listing_Standard list, string id, string label)
        {
            Rect row = list.GetRect(Native == null ? 30f : 36f * Native.Scale);
            list.Gap(CardGap);
            return Button(row, id, label);
        }

        internal static bool Button(Rect row, string id, string label)
        {
            if (!IsVisible(row)) return false;
            return Native == null ? Widgets.ButtonText(row, label) : Native.Button(row, id, label);
        }

        internal static string TextField(Rect row, string id, string value, string placeholder = null)
        {
            if (!IsVisible(row)) return value;
            return Native == null ? Widgets.TextField(row, value) : Native.TextField(row, id, value, placeholder);
        }

        internal static int Choice(Rect row, string id, string label, int selected, string[] options)
        {
            if (!IsVisible(row)) return selected;
            if (Native != null) return Native.Choice(row, id, label, selected, options);
            string choice = selected >= 0 && selected < options.Length ? options[selected] : label;
            if (Widgets.ButtonText(row, choice))
            {
                var entries = new List<FloatMenuOption>(options.Length);
                for (int i = 0; i < options.Length; i++)
                {
                    int option = i;
                    entries.Add(new FloatMenuOption(options[i], () => pendingChoices[id] = option));
                }
                Find.WindowStack.Add(new FloatMenu(entries));
            }
            if (pendingChoices.TryGetValue(id, out int pending))
            {
                pendingChoices.Remove(id);
                return pending;
            }
            return selected;
        }

        private static readonly Dictionary<string, int> pendingChoices = new Dictionary<string, int>(StringComparer.Ordinal);

        internal static bool Card(Listing_Standard list, string anchor, float height, out Rect inner)
        {
            MenuControls.Anchor(list, anchor, height + CardGap);
            Rect card = list.GetRect(height);
            list.Gap(CardGap);
            inner = card.ContractedBy(CardPad);
            if (!IsVisible(card)) return false;
            if (Native == null) Widgets.DrawBox(card);
            else Native.DrawCard(card);
            return true;
        }

        internal static void Quote(Listing_Standard list, string anchor, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            float width = Mathf.Max(1f, list.ColumnWidth - 14f);
            float height = Native == null ? Text.CalcHeight(text, width) : Native.LabelHeight(text, width);
            MenuControls.Anchor(list, anchor, height + CardGap);
            Rect row = list.GetRect(height);
            list.Gap(CardGap);
            if (!IsVisible(row)) return;
            if (Native == null)
            {
                Widgets.DrawBoxSolid(new Rect(row.x, row.y, 3f, row.height), new Color(0.62f, 0.58f, 0.42f));
                Widgets.Label(new Rect(row.x + 10f, row.y, width, height), text);
            }
            else
            {
                Native.Context.Fill(new Rect(row.x, row.y, 3f, row.height), Native.Context.Palette.Marker);
                Native.Context.Label(new Rect(row.x + 10f, row.y, width, height), text,
                    Native.Context.Palette.Muted, TextAnchor.UpperLeft, true);
            }
        }

        internal static float TunedValue(Listing_Standard list, string label, float value, ref string buffer,
            float min, float max, string format, string tooltip, string id = null)
        {
            float height = Native == null ? Mathf.Max(30f, Text.CalcHeight(label, list.ColumnWidth * 0.34f)) :
                Native.NumberHeight(id, label, list.ColumnWidth);
            Rect row = list.GetRect(height);
            list.Gap(CardGap);
            return TunedValue(row, label, value, ref buffer, min, max, format, tooltip, id);
        }

        internal static float TunedValue(Rect row, string label, float value, ref string buffer,
            float min, float max, string format, string tooltip, string id = null)
        {
            if (!IsVisible(row)) return value;
            if (Native != null) return Native.Number(row, id, label, value, ref buffer, min, max, format, tooltip);
            Rect labelRect = new Rect(row.x, row.y, row.width * 0.34f, row.height);
            Widgets.Label(labelRect, label);
            if (!string.IsNullOrEmpty(tooltip)) TooltipHandler.TipRegion(labelRect, tooltip);
            float fieldWidth = 72f;
            Rect field = new Rect(row.xMax - fieldWidth, row.y, fieldWidth, row.height);
            Rect slider = new Rect(labelRect.xMax + 8f, row.y, Mathf.Max(1f, field.x - labelRect.xMax - 16f), row.height);
            float slid = max > min ? Widgets.HorizontalSlider(slider, value, min, max) : min;
            bool sliderMoved = Mathf.Abs(slid - value) > 0.001f;
            if (sliderMoved)
            {
                value = slid;
                buffer = value.ToString(format);
            }
            string typed = buffer;
            Widgets.TextFieldNumeric(field, ref value, ref buffer, min, max);
            if (!sliderMoved && SameNumber(typed, buffer)) buffer = typed;
            return value;
        }


        internal static bool SameNumber(string typed, string parsed)
        {
            float left;
            float right;
            return float.TryParse(typed, out left) && float.TryParse(parsed, out right) && Mathf.Abs(left - right) <= 0.001f;
        }

        internal static void OccurrenceCurve(
            Listing_Standard list,
            string anchor,
            string formula,
            float averageDays,
            float windowDays,
            float today,
            int trust,
            int season,
            Color color)
        {
            const int samples = 48;
            if (!Card(list, anchor, 148f, out Rect plot))
            {
                return;
            }
            Rect graph = new Rect(plot.x, plot.y, plot.width, plot.height);
            Widgets.DrawBoxSolid(graph, new Color(0.08f, 0.08f, 0.08f, 0.55f));
            float span = RHAH_IncidentSchedule.ClampWindowDays(windowDays);
            float axis = AxisChance(formula, averageDays, today, trust, season, span, samples);
            DrawCurve(graph, color, formula, averageDays, today, trust, season, samples, span, axis);
            if (Mouse.IsOver(graph))
            {
                float along = DayAt(graph, Event.current.mousePosition.x, span);
                int dayNumber = Mathf.Clamp(Mathf.CeilToInt(along), 1, Mathf.CeilToInt(span));
                float chance = ChanceOnDay(formula, averageDays, today + dayNumber - 1f, trust, season);
                Widgets.DrawLineVertical(CurvePoint(graph, dayNumber, 0f, span, axis).x, graph.y, graph.height);
                TooltipHandler.TipRegion(graph, () => "RHAH_Menu_Frequency_CurveTip".Translate(
                    dayNumber.ToString(),
                    chance.ToString("P2")), anchor.GetHashCode());
            }
        }

        internal static float ChanceOnDay(string formula, float averageDays, float day, int trust, int season)
        {
            float mean = RHAH_IncidentSchedule.PoolDays(formula, averageDays, day, trust, season);
            return RHAH_IncidentSchedule.DailyOccurrenceChance(mean);
        }

        static float AxisChance(string formula, float averageDays, float today, int trust, int season, float span, int samples)
        {
            float axis = 0f;
            for (int i = 0; i <= samples; i++)
            {
                float day = today + span * i / samples;
                float chance = ChanceOnDay(formula, averageDays, day, trust, season);
                if (chance > axis)
                {
                    axis = chance;
                }
            }

            return axis;
        }

        internal static float DayAt(Rect graph, float mouseX, float windowDays)
        {
            if (graph.width <= 0f)
            {
                return 0f;
            }

            float span = RHAH_IncidentSchedule.ClampWindowDays(windowDays);
            float along = Mathf.Clamp01((mouseX - graph.x) / graph.width);
            return along * span;
        }

        static void DrawCurve(
            Rect graph,
            Color color,
            string formula,
            float averageDays,
            float today,
            int trust,
            int season,
            int samples,
            float span,
            float axis)
        {
            Vector2 last = CurvePoint(graph, 0f, ChanceOnDay(formula, averageDays, today, trust, season), span, axis);
            for (int i = 1; i <= samples; i++)
            {
                float along = span * i / samples;
                float chance = ChanceOnDay(formula, averageDays, today + along, trust, season);
                Vector2 next = CurvePoint(graph, along, chance, span, axis);
                Widgets.DrawLine(last, next, color, 1.5f);
                last = next;
            }
        }

        static Vector2 CurvePoint(Rect graph, float day, float chance, float span, float axisChance)
        {
            float x = graph.x + graph.width * Mathf.Clamp01(span <= 0f ? 0f : day / span);
            float scale = axisChance <= 0f ? 1f : axisChance;
            float y = graph.yMax - graph.height * Mathf.Clamp01(chance / scale);
            return new Vector2(x, y);
        }

        internal static void LitterCurve(Listing_Standard list, string anchor, int minimum, int peak, int maximum)
        {
            RHAH_FertilityRules.ClampLitter(ref minimum, ref peak, ref maximum);
            if (!Card(list, anchor, 148f, out Rect plot))
            {
                return;
            }
            Widgets.DrawBoxSolid(plot, new Color(0.08f, 0.08f, 0.08f, 0.55f));
            int span = Mathf.Max(1, maximum - minimum);
            Vector2 last = LitterPoint(plot, minimum, minimum, peak, maximum, span);
            for (int count = minimum + 1; count <= maximum; count++)
            {
                Vector2 next = LitterPoint(plot, count, minimum, peak, maximum, span);
                Widgets.DrawLine(last, next, new Color(0.78f, 0.62f, 0.38f), 1.5f);
                last = next;
            }

            if (!Mouse.IsOver(plot) || plot.width <= 0f)
            {
                return;
            }

            float along = Mathf.Clamp01((Event.current.mousePosition.x - plot.x) / plot.width);
            int countAt = Mathf.Clamp(Mathf.RoundToInt(minimum + along * span), minimum, maximum);
            Widgets.DrawLineVertical(LitterPoint(plot, countAt, minimum, peak, maximum, span).x, plot.y, plot.height);
            float chance = RHAH_FertilityRules.LitterChance(countAt, minimum, peak, maximum);
            TooltipHandler.TipRegion(plot, () => "RHAH_Menu_Genes_LitterTip".Translate(countAt.ToString(), chance.ToString("P0")), anchor.GetHashCode());
        }

        static Vector2 LitterPoint(Rect graph, int count, int minimum, int peak, int maximum, int span)
        {
            float x = graph.x + graph.width * (count - minimum) / span;
            float chance = RHAH_FertilityRules.LitterChance(count, minimum, peak, maximum);
            return new Vector2(x, graph.yMax - graph.height * Mathf.Clamp01(chance));
        }

        internal static int TuneInt(Listing_Standard list, Dictionary<string, string> buffers, string id, string label, int value, float min, float max, string tip)
        {
            string buffer = buffers.TryGetValue(id, out string stored) ? stored : value.ToString();
            int next = (int)TunedValue(list, label, value, ref buffer, min, max, "0", tip, id);
            buffers[id] = buffer;
            return next;
        }

        internal static float TuneFloat(Listing_Standard list, Dictionary<string, string> buffers, string id, string label, float value, float min, float max, string format, string tip)
        {
            string buffer = buffers.TryGetValue(id, out string stored) ? stored : value.ToString(format);
            float next = TunedValue(list, label, value, ref buffer, min, max, format, tip, id);
            buffers[id] = buffer;
            return next;
        }

    }
}
