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

        public bool Draw(Rect rect, bool value, string label = null, bool enabled = true, float scale = 1f)
        {
            UiInteraction interaction = context.Interact(Id, rect, enabled);
            if (interaction.Clicked) value = !value;
            visual.Step(context, interaction, enabled);
            if (!initialized) { state.Snap(value ? 1f : 0f); initialized = true; }
            float on = state.Step(value ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
            UiPalette palette = context.Palette;
            float unit = Mathf.Max(0f, Mathf.Min(scale, Mathf.Min(rect.width / 52f, rect.height / 28f)));
            float width = 52f * unit;
            float height = 28f * unit;
            Rect track = new Rect(rect.xMax - width, rect.center.y - height * 0.5f, width, height);
            float thumbSize = 20f * unit;
            Rect thumb = new Rect(track.x + (4f + 24f * on) * unit, track.y + 4f * unit, thumbSize, thumbSize);
            using (context.Opacity(visual.Opacity))
            {
                if (!string.IsNullOrEmpty(label)) context.Label(new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - width - 12f * scale), rect.height), label, palette.Ink);
                Color offColor = Color.Lerp(palette.StrongLine, palette.Muted, visual.Hover * 0.25f);
                context.Fill(track, Color.Lerp(offColor, palette.SwitchOn, on), height * 0.5f);
                context.Fill(thumb, Color.Lerp(palette.Raised, palette.Signal, on), thumbSize * 0.5f);
                // The thumb position, not just its color, communicates on/off.
                visual.DrawFocus(context, rect);
            }
            return value;
        }
    }
}
