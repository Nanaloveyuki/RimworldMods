using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiSwitch
    {
        private readonly UiContext context;
        private UiControlVisual visual;
        private UiTween state;
        private UiTween disabled;
        private bool initialized;

        public UiSwitch(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
        }

        public UiId Id { get; }

        public bool Draw(Rect rect, bool value, string label = null, bool enabled = true, float scale = 1f)
        {
            bool originalValue = value;
            UiInteraction interaction = context.Interact(Id, rect, enabled);
            if (interaction.Clicked) value = !value;
            DrawVisual(rect, value, interaction, enabled && GUI.enabled, scale);
            if (value != originalValue) GUI.changed = true;
            if (!string.IsNullOrEmpty(label))
            {
                float width = Mathf.Min(52f * scale, Mathf.Min(rect.width, rect.height * 52f / 28f));
                context.Label(new Rect(rect.x, rect.y, Mathf.Max(0f, rect.width - width - 12f * scale), rect.height),
                    label, Color.Lerp(context.Palette.Ink, context.Palette.SwitchOff, disabled.Value));
            }
            return value;
        }

        internal void DrawVisual(Rect rect, bool value, UiInteraction interaction, bool enabled = true, float scale = 1f)
        {
            visual.Step(context, interaction, enabled);
            if (!initialized)
            {
                state.Snap(value ? 1f : 0f);
                disabled.Snap(enabled ? 0f : 1f);
                initialized = true;
            }
            float on = state.Step(value ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
            float inactive = disabled.Step(enabled ? 0f : 1f, context.Time, UiMotion.Fast, context.ReducedMotion);
            UiPalette palette = context.Palette;
            float unit = Mathf.Max(0f, Mathf.Min(scale, Mathf.Min(rect.width / 52f, rect.height / 28f)));
            float width = 52f * unit;
            float height = 28f * unit;
            Rect track = new Rect(rect.xMax - width, rect.center.y - height * 0.5f, width, height);
            float thumbSize = 20f * unit;
            Rect thumb = new Rect(track.x + (4f + 24f * on) * unit, track.y + 4f * unit, thumbSize, thumbSize);
            if (visual.Focus > 0f)
            {
                Color ring = palette.SwitchFocus;
                ring.a *= visual.Focus;
                Rect outer = new Rect(track.x - 4f * unit, track.y - 4f * unit, track.width + 8f * unit, track.height + 8f * unit);
                context.Fill(outer, ring, outer.height * 0.5f);
                Rect gap = new Rect(track.x - 2f * unit, track.y - 2f * unit, track.width + 4f * unit, track.height + 4f * unit);
                context.Fill(gap, palette.Surface, gap.height * 0.5f);
            }
            Color offColor = Color.Lerp(palette.SwitchOff, palette.SwitchHover, visual.Hover);
            context.Fill(track, Color.Lerp(Color.Lerp(offColor, palette.SwitchOn, on), palette.SwitchDisabled, inactive), height * 0.5f);
            context.Fill(thumb, Color.Lerp(Color.Lerp(palette.SwitchThumb, palette.AccentInk, on), palette.SwitchOff, inactive), thumbSize * 0.5f);
        }
    }
}
