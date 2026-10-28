using System;
using UnityEngine;

namespace IrisMenus.UI
{
    /// <summary>Read-only preview surface. Observe after its trigger and draw outside content clips.</summary>
    public sealed class UiHoverCard
    {
        private readonly UiContext context;
        private UiTween visibility, entrance;
        private Rect anchorScreen, panelScreen;
        private string text = string.Empty;
        private bool eligible, requested, suppressed;
        private double enteredAt, leftAt = double.NegativeInfinity;
        private int observedFrame = -1;
        private float measuredWidth = -1f, measuredFont, measuredScale, naturalHeight, scroll;
        private FontStyle measuredStyle;
        private float delay = 0.6f;

        public UiHoverCard(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public bool IsVisible => requested || visibility.Value > 0f;
        public float Delay
        {
            get => delay;
            set
            {
                if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f) throw new ArgumentOutOfRangeException(nameof(value));
                delay = value;
            }
        }

        public void Observe(Rect anchor, string content, UiId targetId = default(UiId), bool enabled = true)
        {
            observedFrame = context.Frame;
            bool keyboard = context.IsKeyboardInputAllowed && context.FocusVisible && targetId != default(UiId) && context.IsFocused(targetId);
            bool overTrigger = enabled && context.IsInputAllowed && !string.IsNullOrEmpty(content) && (context.Hit(anchor) || keyboard);
            bool overPanel = enabled && context.IsPointerInputAllowed && requested && context.Event != null &&
                panelScreen.Contains(UiCoordinates.ToScreen(context.Event.mousePosition));
            bool changed = !string.Equals(text, content, StringComparison.Ordinal);
            if (overTrigger)
            {
                if (!eligible || changed)
                {
                    enteredAt = context.Time;
                    if (changed) { measuredWidth = -1f; scroll = 0f; }
                }
                eligible = true;
                leftAt = double.NegativeInfinity;
                anchorScreen = UiCoordinates.ToScreen(anchor);
                text = content;
                requested = !suppressed && (requested || context.Time - enteredAt >= delay);
            }
            else if (overPanel)
            {
                leftAt = double.NegativeInfinity;
            }
            else
            {
                eligible = false;
                suppressed = false;
                if (double.IsNegativeInfinity(leftAt)) leftAt = context.Time;
                if (!enabled || !context.IsInputAllowed || context.Time - leftAt >= 0.3) requested = false;
            }
            Event current = context.Event;
            if (current != null && ((context.IsPointerInputAllowed && current.rawType == EventType.MouseDown) ||
                (context.IsKeyboardInputAllowed && current.rawType == EventType.KeyDown && current.keyCode == KeyCode.Escape))) Dismiss();
        }

        public void Dismiss()
        {
            requested = false;
            suppressed = true;
        }

        public void DrawOverlay(Rect bounds, float scale = 1f, float preferredWidth = 320f)
        {
            if (observedFrame != context.Frame || !context.IsInputAllowed)
            {
                requested = false;
                eligible = false;
                suppressed = false;
                leftAt = double.NegativeInfinity;
            }
            float amount = visibility.Step(requested ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
            float entry = requested ? entrance.Step(1f, context.Time, UiMotion.Standard, context.ReducedMotion) : entrance.Value;
            if (amount <= 0f)
            {
                if (!requested) entrance.Snap(0f);
                return;
            }
            if (string.IsNullOrEmpty(text) || bounds.width <= 0f || bounds.height <= 0f) return;
            float unit = Mathf.Max(0.01f, scale);
            float inset = Mathf.Min(8f * unit, Mathf.Min(bounds.width, bounds.height) * 0.05f);
            bounds = new Rect(bounds.x + inset, bounds.y + inset, bounds.width - inset * 2f, bounds.height - inset * 2f);
            float width = Mathf.Min(preferredWidth * unit, bounds.width);
            float padding = Mathf.Min(16f * unit, width * 0.2f);
            float textWidth = Mathf.Max(1f, width - padding * 2f);
            if (measuredWidth != textWidth || measuredFont != context.FontSize || measuredStyle != context.FontStyle || measuredScale != unit)
            {
                measuredWidth = textWidth;
                measuredFont = context.FontSize;
                measuredStyle = context.FontStyle;
                measuredScale = unit;
                naturalHeight = UiTextMetrics.Height(context, text, textWidth);
            }
            float height = Mathf.Min(bounds.height, naturalHeight + padding * 2f);
            Rect anchor = UiCoordinates.FromScreen(anchorScreen);
            float gap = 4f * unit;
            float belowSpace = bounds.yMax - anchor.yMax - gap;
            float aboveSpace = anchor.y - bounds.y - gap;
            bool below = belowSpace >= height || belowSpace >= aboveSpace;
            float x = Mathf.Clamp(anchor.x, bounds.x, bounds.xMax - width);
            float y = below ? anchor.yMax + gap : anchor.y - height - gap;
            y = Mathf.Clamp(y + (1f - entry) * (below ? -4f : 4f) * unit, bounds.y, bounds.yMax - height);
            Rect panel = new Rect(x, y, width, height);
            panelScreen = UiCoordinates.ToScreen(panel);
            Rect body = new Rect(panel.x + padding, panel.y + padding, textWidth, Mathf.Max(1f, height - padding * 2f));
            float maximumScroll = Mathf.Max(0f, naturalHeight - body.height);
            Event current = context.Event;
            if (requested && maximumScroll > 0f && current != null && current.type == EventType.ScrollWheel && context.Hit(panel))
            {
                scroll = Mathf.Clamp(scroll + current.delta.y * 20f * unit, 0f, maximumScroll);
                context.Consume();
            }
            scroll = Mathf.Clamp(scroll, 0f, maximumScroll);
            UiPalette palette = context.Palette;
            using (context.Opacity(amount))
            {
                context.Fill(new Rect(panel.x + 2f * unit, panel.y + 3f * unit, panel.width, panel.height), palette.Shadow);
                context.Fill(panel, palette.Raised);
                context.Border(panel, palette.StrongLine, unit);
                using (context.Clip(body))
                    context.Label(new Rect(0f, -scroll, body.width, Mathf.Max(body.height, naturalHeight)), text, palette.Ink, TextAnchor.UpperLeft, true);
                float barWidth = panel.width * 0.5f * entry;
                context.Fill(new Rect(panel.x, panel.y, barWidth, 3f * unit), palette.Signal);
                float marker = Mathf.Min(32f * unit * entry, Mathf.Max(0f, barWidth - 4f * unit));
                context.Triangle(new Vector2(panel.x + marker - 4f * unit * entry, panel.y + 3f * unit),
                    new Vector2(panel.x + marker + 4f * unit * entry, panel.y + 3f * unit),
                    new Vector2(panel.x + marker, panel.y + 7f * unit), palette.Signal);
                if (maximumScroll > 0f)
                {
                    float track = body.height;
                    float thumb = Mathf.Max(12f * unit, track * body.height / naturalHeight);
                    context.Fill(new Rect(panel.xMax - 5f * unit, body.y + (track - thumb) * scroll / maximumScroll,
                        2f * unit, thumb), palette.Muted);
                }
            }
        }
    }
}
