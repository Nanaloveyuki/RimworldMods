using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiCheckbox
    {
        private readonly UiContext context;
        private UiControlVisual visual;
        private UiTween state;
        private bool initialized;

        public UiCheckbox(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
        }

        public UiId Id { get; }

        public bool Draw(Rect rect, bool value, string label = null, bool enabled = true)
        {
            UiInteraction interaction = context.Interact(Id, rect, enabled);
            if (interaction.Clicked) value = !value;
            visual.Step(context, interaction, enabled);
            if (!initialized) { state.Snap(value ? 1f : 0f); initialized = true; }
            float check = state.Step(value ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
            UiPalette palette = context.Palette;
            float size = Mathf.Max(0f, Mathf.Min(20f, Mathf.Min(rect.width, rect.height - 4f)));
            Rect box = new Rect(rect.x, rect.center.y - size * 0.5f, size, size);
            using (context.Opacity(visual.Opacity))
            {
                Color normal = visual.Background(palette, palette.Sunken);
                Color background = Color.Lerp(normal, palette.Selected, check);
                background = Color.Lerp(background, palette.Signal, visual.Press * 0.16f);
                context.Fill(box, background);
                context.Border(box, Color.Lerp(palette.StrongLine, palette.Signal, Mathf.Max(check, visual.Focus)));
                if (check > 0f)
                {
                    Vector2 start = new Vector2(box.x + size * 0.23f, box.y + size * 0.5f);
                    Vector2 elbow = new Vector2(box.x + size * 0.43f, box.y + size * 0.7f);
                    Vector2 end = new Vector2(box.x + size * 0.79f, box.y + size * 0.28f);
                    Color ink = palette.OnSelected;
                    ink.a *= check;
                    float first = Mathf.Clamp01(check * 2.5f);
                    float second = Mathf.Clamp01((check - 0.4f) / 0.6f);
                    context.Line(start, Vector2.Lerp(start, elbow, first), ink, 2f);
                    if (second > 0f) context.Line(elbow, Vector2.Lerp(elbow, end, second), ink, 2f);
                }
                if (!string.IsNullOrEmpty(label)) context.Label(new Rect(box.xMax + 10f, rect.y, Mathf.Max(0f, rect.width - size - 10f), rect.height), label, palette.Ink);
                visual.DrawFocus(context, rect);
            }
            return value;
        }
    }
}
