using System;
using System.Globalization;
using UnityEngine;

namespace IrisMenus.UI
{
    /// <summary>Nonblocking inline loading feedback. A null value explicitly means unknown progress.</summary>
    public sealed class UiLoader
    {
        private readonly UiContext context;
        private int shownPercent = -1;
        private string percentage;

        public UiLoader(UiContext context) { this.context = context ?? throw new ArgumentNullException(nameof(context)); }

        public void Draw(Rect rect, string label, float? value = null, float scale = 1f)
        {
            if (!context.IsRectVisible(rect)) return;
            bool determinate = value.HasValue && !float.IsNaN(value.Value) && !float.IsInfinity(value.Value);
            float progress = determinate ? Mathf.Clamp01(value.Value) : 0f;
            float height = Mathf.Min(6f * scale, rect.height);
            Rect track = new Rect(rect.x, rect.yMax - height, rect.width, height);
            context.Fill(track, context.Palette.StrongLine);
            if (determinate) context.Fill(new Rect(track.x, track.y, track.width * progress, track.height), context.Palette.Signal);
            else
            {
                float phase = context.ReducedMotion ? 0.3f : (float)(context.Time % 1.4d / 1.4d);
                float left = Mathf.Max(0f, phase * 1.4f - 0.4f);
                float right = Mathf.Min(1f, phase * 1.4f);
                context.Fill(new Rect(track.x + left * track.width, track.y, (right - left) * track.width, track.height), context.Palette.Signal);
            }
            if (rect.height <= height + 4f * scale) return;
            Rect text = new Rect(rect.x, rect.y, rect.width, Mathf.Max(0f, rect.height - height - 6f * scale));
            if (determinate)
            {
                int percent = Mathf.RoundToInt(progress * 100f);
                if (percent != shownPercent) { shownPercent = percent; percentage = percent.ToString(CultureInfo.InvariantCulture) + "%"; }
                float numberWidth = Mathf.Min(64f * scale, text.width);
                context.Label(new Rect(text.xMax - numberWidth, text.y, numberWidth, text.height), percentage,
                    context.Palette.Ink, TextAnchor.MiddleRight);
                text.width = Mathf.Max(0f, text.width - numberWidth - 8f * scale);
            }
            context.Label(text, label, context.Palette.Muted);
        }
    }
}
