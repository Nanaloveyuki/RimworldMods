using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiAccordion
    {
        private readonly UiContext context;
        private UiTween expansion;
        private UiTween hover;
        private UiTween press;
        private UiTween focus;

        public UiAccordion(UiContext context, bool initiallyExpanded = false)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
            expansion = new UiTween(initiallyExpanded ? 1f : 0f);
        }

        public UiId Id { get; }
        public float Expansion => expansion.Value;

        // Measure before laying out subsequent siblings. Content height is its uncollapsed height.
        public float Measure(bool expanded, float naturalContentHeight, float headerHeight = 44f)
        {
            ValidateHeight(naturalContentHeight, nameof(naturalContentHeight), true);
            ValidateHeight(headerHeight, nameof(headerHeight), false);
            return headerHeight + naturalContentHeight * expansion.Step(expanded ? 1f : 0f,
                context.Time, expanded ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
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
                    next ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
                float h = hover.Step(interaction.Hovered ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                float p = press.Step(interaction.Pressed ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                float f = focus.Step(interaction.Focused && context.FocusVisible ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
                UiPalette palette = context.Palette;
                using (context.Opacity(enabled ? 1f : 0.42f))
                {
                    context.Fill(header, palette.Raised);
                    Color hoverColor = palette.Hover;
                    hoverColor.a *= Mathf.Max(h * 0.65f, p);
                    context.Fill(header, hoverColor);
                    Color focusColor = palette.Signal;
                    focusColor.a *= f;
                    context.Border(new Rect(1f, 1f, Mathf.Max(0f, header.width - 2f), Mathf.Max(0f, header.height - 2f)), focusColor);
                    context.Label(new Rect(14f, 0f, Mathf.Max(0f, rect.width - 54f), headerHeight), title, palette.Ink);
                    float angle = amount * Mathf.PI * 0.5f;
                    float cosine = Mathf.Cos(angle), sine = Mathf.Sin(angle);
                    Vector2 center = new Vector2(rect.width - 23f, headerHeight * 0.5f);
                    Vector2 a = Rotate(new Vector2(-3f, -5f), cosine, sine) + center;
                    Vector2 b = Rotate(new Vector2(2f, 0f), cosine, sine) + center;
                    Vector2 c = Rotate(new Vector2(-3f, 5f), cosine, sine) + center;
                    context.Line(a, b, palette.Muted, 1.6f);
                    context.Line(b, c, palette.Muted, 1.6f);
                }
                float visibleHeight = Mathf.Min(naturalContentHeight * amount, Mathf.Max(0f, rect.height - headerHeight));
                if (visibleHeight > 0f)
                {
                    using (context.Input(enabled && next))
                    using (context.Opacity(amount))
                    using (context.Clip(new Rect(0f, headerHeight, rect.width, visibleHeight)))
                    using (context.Clip(new Rect(0f, 0f, rect.width, naturalContentHeight)))
                        drawContent(new Rect(0f, 0f, rect.width, naturalContentHeight));
                }
                return next;
            }
        }

        private static Vector2 Rotate(Vector2 value, float cosine, float sine)
            => new Vector2(value.x * cosine - value.y * sine, value.x * sine + value.y * cosine);

        private static void ValidateHeight(float value, string name, bool allowZero)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f || (!allowZero && value == 0f))
                throw new ArgumentOutOfRangeException(name);
        }
    }
}
