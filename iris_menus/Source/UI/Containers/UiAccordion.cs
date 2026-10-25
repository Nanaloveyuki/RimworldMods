using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiAccordion
    {
        private readonly UiContext context;
        private UiTween expansion;
        private UiTween marker;
        private UiTween hover;
        private UiTween press;
        private UiTween focus;

        public UiAccordion(UiContext context, bool initiallyExpanded = false)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
            expansion = new UiTween(initiallyExpanded ? 1f : 0f);
            marker = new UiTween(initiallyExpanded ? 1f : 0f);
        }

        public UiId Id { get; }
        public float Expansion => expansion.Value;

        // Measure before laying out subsequent siblings. Content height is its uncollapsed height.
        public float Measure(bool expanded, float naturalContentHeight, float headerHeight = 44f)
        {
            ValidateHeight(naturalContentHeight, nameof(naturalContentHeight), true);
            ValidateHeight(headerHeight, nameof(headerHeight), false);
            return headerHeight + naturalContentHeight * expansion.Step(expanded ? 1f : 0f,
                context.Time, UiMotion.Enter, context.ReducedMotion);
        }

        // The callback is caller-cached. Its rect is always natural size in local coordinates;
        // clipping, not a squeezed rect, hides the collapsed portion and its input targets.
        public bool Draw(Rect rect, string title, bool expanded, float naturalContentHeight,
            Action<Rect> drawContent, bool enabled = true, float headerHeight = 44f)
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (drawContent == null) throw new ArgumentNullException(nameof(drawContent));
            Measure(expanded, naturalContentHeight, headerHeight);
            using (context.Clip(rect))
            {
                Rect header = new Rect(0f, 0f, rect.width, headerHeight);
                UiInteraction interaction = context.Interact(Id, header, enabled);
                bool next = expanded;
                if (interaction.Clicked) next = !next;
                if (interaction.Focused && enabled)
                {
                    if (context.ConsumeKey(KeyCode.LeftArrow)) next = false;
                    else if (context.ConsumeKey(KeyCode.RightArrow)) next = true;
                }
                float amount = expansion.Step(next ? 1f : 0f, context.Time,
                    UiMotion.Enter, context.ReducedMotion);
                float h = hover.Step(interaction.Hovered ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                float p = press.Step(interaction.Pressed ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                float f = focus.Step(interaction.Focused && context.FocusVisible ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                float mark = marker.Step(next ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                UiPalette palette = context.Palette;
                using (context.Opacity(enabled ? 1f : 0.42f))
                {
                    Color hoverColor = palette.Ink;
                    hoverColor.a *= 0.05f * Mathf.Max(h, p);
                    context.Fill(header, hoverColor);
                    context.Fill(new Rect(0f, 0f, rect.width, 1f), palette.Line);
                    context.Fill(new Rect(0f, Mathf.Max(0f, rect.height - 1f), rect.width, 1f), palette.Line);
                    Color focusColor = palette.Signal;
                    focusColor.a *= f;
                    context.Border(new Rect(1f, 1f, Mathf.Max(0f, header.width - 2f), Mathf.Max(0f, header.height - 2f)), focusColor);
                    FontStyle previousStyle = context.FontStyle;
                    try
                    {
                        context.FontStyle = next ? FontStyle.Bold : FontStyle.Normal;
                        context.Label(new Rect(16f, 0f, Mathf.Max(0f, rect.width - 60f), headerHeight), title, palette.Ink);
                    }
                    finally { context.FontStyle = previousStyle; }
                    Vector2 center = new Vector2(rect.width - 24f, headerHeight * 0.5f);
                    context.Line(center + new Vector2(-5f, 0f), center + new Vector2(5f, 0f), palette.Muted, 1.5f);
                    float angle = (1f - mark) * Mathf.PI * 0.5f;
                    Vector2 stroke = new Vector2(Mathf.Cos(angle) * 5f, Mathf.Sin(angle) * 5f);
                    Color rotatingInk = palette.Muted;
                    rotatingInk.a *= 1f - mark;
                    context.Line(center - stroke, center + stroke, rotatingInk, 1.5f);
                }
                float visibleHeight = Mathf.Min(naturalContentHeight * amount, Mathf.Max(0f, rect.height - headerHeight));
                if (visibleHeight > 0f)
                {
                    using (context.Input(enabled && next))
                    using (context.Clip(new Rect(0f, headerHeight, rect.width, visibleHeight)))
                    using (context.Clip(new Rect(0f, 0f, rect.width, naturalContentHeight)))
                        drawContent(new Rect(0f, 0f, rect.width, naturalContentHeight));
                }
                return next;
            }
        }

        private static void ValidateHeight(float value, string name, bool allowZero)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || (!allowZero && value == 0f))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
