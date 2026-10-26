using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiSlider
    {
        private readonly UiContext context;
        private readonly float minimum;
        private readonly float maximum;
        private readonly float step;
        private UiControlVisual visual;
        private UiTween thumb;
        private UiTween dragAccent;
        private bool initialized;
        private bool dragging;
        private float dragWidth;
        private float dragValue;
        private Vector2 lastMouse;

        public UiSlider(UiContext context, float minimum = 0f, float maximum = 1f, float step = 0f)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (float.IsNaN(minimum) || float.IsInfinity(minimum)) throw new ArgumentOutOfRangeException(nameof(minimum));
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= minimum || float.IsInfinity(maximum - minimum)) throw new ArgumentOutOfRangeException(nameof(maximum));
            if (float.IsNaN(step) || float.IsInfinity(step) || step < 0f) throw new ArgumentOutOfRangeException(nameof(step));
            this.minimum = minimum;
            this.maximum = maximum;
            this.step = step;
            Id = context.CreateId();
        }

        public UiId Id { get; }
        public bool IsDragging => dragging;
        public float Minimum => minimum;
        public float Maximum => maximum;

        public float Draw(Rect rect, float value, bool enabled = true, float scale = 1f) =>
            DrawHosted(rect, rect, value, enabled, scale);

        internal float DrawHosted(Rect rect, Rect inputRect, float value, bool enabled = true, float scale = 1f)
        {
            value = NormalizeValue(value);
            float previous = value;
            Event current = context.Event;
            EventType type = current != null ? current.type : EventType.Ignore;
            Vector2 mouse = current != null ? UiCoordinates.ToScreen(current.mousePosition) : Vector2.zero;
            bool captured = context.IsCaptured(Id);
            UiInteraction interaction = context.Interact(Id, inputRect, enabled, false);
            float unit = Mathf.Max(0f, Mathf.Min(scale, Mathf.Min(rect.height / 20f, rect.width / 12f)));
            float halfWidth = 6f * unit;
            float trackWidth = Mathf.Max(1f, rect.width - halfWidth * 2f);
            if (enabled && !captured && context.IsCaptured(Id) && type == EventType.MouseDown)
            {
                dragging = true;
                dragWidth = Mathf.Max(0.01f, Mathf.Abs(UiCoordinates.ToScreen(new Vector2(rect.x + trackWidth, rect.y)).x - UiCoordinates.ToScreen(rect.position).x));
                lastMouse = mouse;
                float center = rect.x + halfWidth + (value - minimum) / (maximum - minimum) * trackWidth;
                // A thumb grab preserves its offset; clicking the track jumps to that position.
                if (Mathf.Abs(current.mousePosition.x - center) > halfWidth + 3f * unit)
                    value = NormalizeValue(minimum + Mathf.Clamp01((current.mousePosition.x - rect.x - halfWidth) / trackWidth) * (maximum - minimum));
                dragValue = value;
            }
            if (dragging && enabled && captured && context.IsInputAllowed && (type == EventType.MouseDrag || type == EventType.MouseUp))
            {
                dragValue = Mathf.Clamp(dragValue + (mouse.x - lastMouse.x) / dragWidth * (maximum - minimum), minimum, maximum);
                lastMouse = mouse;
                value = NormalizeValue(dragValue);
                if (type == EventType.MouseDrag) context.Consume();
            }
            if (!enabled || !context.IsCaptured(Id)) dragging = false;
            if (enabled && interaction.Focused && context.IsInputAllowed)
            {
                float increment = step > 0f ? step : (maximum - minimum) * 0.01f;
                if (current != null && current.shift) increment *= 10f;
                if (context.ConsumeKey(KeyCode.LeftArrow) || context.ConsumeKey(KeyCode.DownArrow)) value -= increment;
                else if (context.ConsumeKey(KeyCode.RightArrow) || context.ConsumeKey(KeyCode.UpArrow)) value += increment;
                else if (context.ConsumeKey(KeyCode.PageDown)) value -= increment * 10f;
                else if (context.ConsumeKey(KeyCode.PageUp)) value += increment * 10f;
                else if (context.ConsumeKey(KeyCode.Home)) value = minimum;
                else if (context.ConsumeKey(KeyCode.End)) value = maximum;
                value = NormalizeValue(value);
            }
            visual.Step(context, interaction, enabled);
            float target = (value - minimum) / (maximum - minimum);
            if (!initialized || dragging) { thumb.Snap(target); initialized = true; }
            float position = thumb.Step(target, context.Time, UiMotion.Standard, context.ReducedMotion);
            float accent = dragAccent.Step(dragging ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
            float x = rect.x + halfWidth + trackWidth * position;
            Rect track = new Rect(rect.x + halfWidth, rect.center.y - 2f * unit, trackWidth, 4f * unit);
            UiPalette palette = context.Palette;
            using (context.Opacity(visual.Opacity))
            {
                context.Fill(track, palette.Line);
                context.Fill(new Rect(track.x, track.y, track.width * position, track.height), palette.Ink);
                Rect handle = new Rect(x - halfWidth, rect.center.y - 10f * unit, 12f * unit, 20f * unit);
                context.Fill(new Rect(handle.x - 2f * unit, handle.y - 2f * unit, handle.width + 4f * unit, handle.height + 4f * unit), palette.Surface);
                context.Fill(handle, palette.Selected);
                Color centerInk = palette.AccentInk;
                centerInk.a *= accent;
                context.Fill(new Rect(x - unit, handle.y + 4f * unit, 2f * unit, 12f * unit), centerInk);
                visual.DrawFocus(context, rect);
            }
            if (value != previous) GUI.changed = true;
            return value;
        }

        private float NormalizeValue(float value)
        {
            if (float.IsNaN(value)) return minimum;
            value = Mathf.Clamp(value, minimum, maximum);
            if (step <= 0f || value == maximum) return value;
            double units = Math.Round(((double)value - minimum) / step, MidpointRounding.AwayFromZero);
            return Mathf.Clamp((float)(minimum + units * step), minimum, maximum);
        }
    }
}
