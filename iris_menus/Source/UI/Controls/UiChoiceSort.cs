using System;
using UnityEngine;

namespace IrisMenus.UI
{
    /// <summary>A labeled choice bar for sorting/grouping and other small exclusive option sets.</summary>
    public sealed class UiChoiceSort
    {
        private readonly UiContext context;
        private readonly UiSegments segments;
        private string label, shortLabel;
        private float measuredWidth = -1f, measuredFont;

        public UiChoiceSort(UiContext context, string[] options)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            segments = new UiSegments(context, options);
        }

        public UiId ItemId(int index) => segments.GetItemId(index);
        public void SetOptions(string[] options) => segments.SetOptions(options);

        public int Draw(Rect rect, string title, int selected, bool enabled = true, float scale = 1f)
        {
            float labelWidth = Mathf.Min(220f * scale, rect.width * 0.35f);
            if (label != title || measuredWidth != labelWidth || measuredFont != context.FontSize)
            {
                label = title;
                measuredWidth = labelWidth;
                measuredFont = context.FontSize;
                shortLabel = UiTextMetrics.Ellipsize(context, title, labelWidth - 8f * scale);
            }
            context.Label(new Rect(rect.x, rect.y, labelWidth, rect.height), shortLabel, context.Palette.Muted);
            return segments.Draw(new Rect(rect.x + labelWidth, rect.y, Mathf.Max(0f, rect.width - labelWidth), rect.height), selected, enabled, scale);
        }
    }
}
