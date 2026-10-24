using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiSwitch
    {
        private readonly UiContext context;
        private UiControlVisual visual;
        private UiTween state;
        private bool initialized;

        public UiSwitch(UiContext context)
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
            float on = state.Step(value ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
            UiPalette palette = context.Palette;
            float width = Mathf.Max(0f, Mathf.Min(44f, rect.width));
            float height = Mathf.Min(width, Mathf.Min(24f, Mathf.Max(0f, rect.height - 4f)));
            Rect track = new Rect(rect.xMax - width, rect.center.y - height * 0.5f, width, height);
            float thumbSize = Mathf.Max(0f, height - 6f);
            float thumbWidth = Mathf.Max(0f, Mathf.Min(width - 6f, thumbSize + visual.Press * 3f));
            float travel = Mathf.Max(0f, width - thumbWidth - 6f);
            Rect thumb = new Rect(track.x + 3f + travel * on, track.center.y - thumbSize * 0.5f, Mathf.Max(0f, thumbWidth), thumbSize);
            using (context.Opacity(visual.Opacity))
            {
                if (!string.IsNullOrEmpty(label)) context.Label(new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - width - 12f), rect.height), label, palette.Ink);
                Color offColor = Color.Lerp(palette.StrongLine, palette.Muted, visual.Hover * 0.25f);
                context.Fill(track, Color.Lerp(offColor, palette.SwitchOn, on), height * 0.5f);
                context.Fill(thumb, Color.Lerp(palette.Surface, palette.OnSelected, on), thumbSize * 0.5f);
                // The thumb position, not just its color, communicates on/off.
                visual.DrawFocus(context, rect);
            }
            return value;
        }
    }
}
