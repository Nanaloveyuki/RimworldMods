using System;
using UnityEngine;
using Verse;

namespace IrisMenus.UI
{
    internal static class UiTextMetrics
    {
#if !RIMWORLD_1_7
        private static readonly GUIStyle style = new GUIStyle();
        private static readonly GUIContent content = new GUIContent();

        private static void Prepare(UiContext context, string text, bool wrap)
        {
            style.font = Text.CurFontStyle.font;
            style.fontSize = Mathf.RoundToInt(context.FontSize);
            style.fontStyle = context.FontStyle;
            style.wordWrap = wrap;
            style.richText = false;
            content.text = text ?? string.Empty;
        }
#endif

        internal static float Width(UiContext context, string text)
        {
            if (string.IsNullOrEmpty(text)) return 0f;
#if RIMWORLD_1_7
            return UnityStubs.Gui.GuiBackend.Current?.MeasureText(1, context.FontSize, context.FontStyle, text, -1f).x ?? 0f;
#else
            Prepare(context, text, false);
            return style.CalcSize(content).x;
#endif
        }

        internal static float Height(UiContext context, string text, float width)
        {
            if (string.IsNullOrEmpty(text) || width <= 0f) return 0f;
#if RIMWORLD_1_7
            return UnityStubs.Gui.GuiBackend.Current?.MeasureText(1, context.FontSize, context.FontStyle, text, width).y ?? 0f;
#else
            Prepare(context, text, true);
            return style.CalcHeight(content, width);
#endif
        }

        // Callers cache this result with their measured layout, rather than allocating every repaint.
        internal static string Ellipsize(UiContext context, string text, float width, bool middle = false)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (width <= 0f) return string.Empty;
            if (Width(context, text) <= width) return text;
            const string ellipsis = "...";
            if (Width(context, ellipsis) > width) return string.Empty;
            int low = 0, high = text.Length - 1;
            string best = ellipsis;
            while (low <= high)
            {
                int keep = low + (high - low) / 2;
                string candidate;
                if (middle)
                {
                    int head = (keep + 1) / 2, tail = keep / 2;
                    if (head > 0 && head < text.Length && char.IsHighSurrogate(text[head - 1])) head--;
                    int end = text.Length - tail;
                    if (end < text.Length && char.IsLowSurrogate(text[end])) end++;
                    candidate = text.Substring(0, head) + ellipsis + text.Substring(end);
                }
                else
                {
                    int head = keep;
                    if (head > 0 && char.IsHighSurrogate(text[head - 1])) head--;
                    candidate = text.Substring(0, head) + ellipsis;
                }
                if (Width(context, candidate) <= width) { best = candidate; low = keep + 1; }
                else high = keep - 1;
            }
            return best;
        }
    }
}
