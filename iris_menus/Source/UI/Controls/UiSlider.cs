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

        public float Draw(Rect rect, float value, bool enabled = true)
        {
            value = NormalizeValue(value);
            Event current = context.Event;
            EventType type = current != null ? current.type : EventType.Ignore;
            Vector2 mouse = current != null ? GUIUtility.GUIToScreenPoint(current.mousePosition) : Vector2.zero;
            bool captured = context.IsCaptured(Id);
            UiInteraction interaction = context.Interact(Id, rect, enabled, false);
            float radius = Mathf.Max(0f, Mathf.Min(8f, Mathf.Min(rect.height, rect.width) * 0.25f));
            float trackWidth = Mathf.Max(1f, rect.width - radius * 2f);
            if (enabled && !captured && context.IsCaptured(Id) && type == EventType.MouseDown)
            {
                dragging = true;
                dragWidth = trackWidth;
                lastMouse = mouse;
                float center = rect.x + radius + (value - minimum) / (maximum - minimum) * trackWidth;
                // A thumb grab preserves its offset; clicking the track jumps to that position.
                if (Mathf.Abs(current.mousePosition.x - center) > radius + 3f)
                    value = NormalizeValue(minimum + Mathf.Clamp01((current.mousePosition.x - rect.x - radius) / trackWidth) * (maximum - minimum));
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
            float x = rect.x + radius + trackWidth * position;
            Rect track = new Rect(rect.x + radius, rect.center.y - 2f, trackWidth, 4f);
            UiPalette palette = context.Palette;
            using (context.Opacity(visual.Opacity))
            {
                context.Fill(track, Color.Lerp(palette.StrongLine, palette.Muted, visual.Hover * 0.25f), 2f);
                context.Fill(new Rect(track.x, track.y, track.width * position, track.height), palette.Signal, 2f);
                float haloRadius = radius + 4f + visual.Press * 2f;
                Color halo = palette.Signal;
                halo.a *= (visual.Hover * 0.12f + visual.Focus * 0.15f + visual.Press * 0.15f);
                context.Fill(new Rect(x - haloRadius, rect.center.y - haloRadius, haloRadius * 2f, haloRadius * 2f), halo, haloRadius);
                float thumbRadius = radius + visual.Press;
                context.Fill(new Rect(x - thumbRadius, rect.center.y - thumbRadius, thumbRadius * 2f, thumbRadius * 2f), palette.Signal, thumbRadius);
                visual.DrawFocus(context, rect);
            }
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
