using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiTooltip
    {
        private readonly UiContext context;
        private float delay;
        private UiTween visibility;
        private Rect anchorScreen;
        private string text = string.Empty;
        private bool eligible;
        private bool suppressed;
        private bool requested;
        private double enteredAt;
        private int observedFrame = -1;

        public UiTooltip(UiContext context, float delay = 0.5f)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (float.IsNaN(delay) || float.IsInfinity(delay) || delay < 0f) throw new ArgumentOutOfRangeException(nameof(delay));
            Delay = delay;
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

        // Observe after the target control; targetId enables the same tooltip from keyboard focus.
        // This is not a second Interact call and does not participate in focus or mouse capture.
        public void Observe(Rect anchor, string content, UiId targetId = default(UiId), bool enabled = true)
        {
            observedFrame = context.Frame;
            bool over = enabled && !string.IsNullOrEmpty(content) && context.IsInputAllowed &&
                (context.Hit(anchor) || (!targetId.Equals(default(UiId)) && context.IsFocused(targetId)));
            if (!over)
            {
                eligible = false;
                suppressed = false;
                requested = false;
                return;
            }
            if (!eligible || !string.Equals(text, content, StringComparison.Ordinal))
            {
                enteredAt = context.Time;
                eligible = true;
            }
            Vector2 screen = GUIUtility.GUIToScreenPoint(anchor.position);
            anchorScreen = new Rect(screen.x, screen.y, anchor.width, anchor.height);
            text = content;
            Event current = context.Event;
            if (current != null && (current.rawType == EventType.MouseDown ||
                (current.rawType == EventType.KeyDown && current.keyCode == KeyCode.Escape))) suppressed = true;
            requested = !suppressed && (visibility.Value > 0f || context.Time - enteredAt >= delay);
        }

        // Keeps the exiting text/anchor while fading, even when the target is no longer drawn.
        public void Dismiss()
        {
            requested = false;
            suppressed = true;
        }

        // Draw after all content, outside its scroll/clip groups. Size is explicit to keep
        // measurement and layout caller-owned; long text wraps inside the constrained rect.
        public void DrawOverlay(Rect bounds, Vector2 size)
        {
            if (observedFrame != context.Frame || !context.IsInputAllowed)
            {
                requested = false;
                eligible = false;
                suppressed = false;
            }
            float amount = visibility.Step(requested ? 1f : 0f, context.Time, requested ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
            if (amount <= 0f || string.IsNullOrEmpty(text) || bounds.width <= 0f || bounds.height <= 0f) return;
            float width = Mathf.Clamp(size.x, 0f, bounds.width);
            float height = Mathf.Clamp(size.y, 0f, bounds.height);
            Vector2 local = GUIUtility.ScreenToGUIPoint(anchorScreen.position);
            Rect anchor = new Rect(local.x, local.y, anchorScreen.width, anchorScreen.height);
            bool below = anchor.yMax + height + 8f <= bounds.yMax || anchor.y - height - 8f < bounds.y;
            float x = Mathf.Clamp(anchor.center.x - width * 0.5f, bounds.x, bounds.xMax - width);
            float y = below ? anchor.yMax + 8f : anchor.y - height - 8f;
            y = Mathf.Clamp(y + (1f - amount) * (below ? -4f : 4f), bounds.y, bounds.yMax - height);
            Rect panel = new Rect(x, y, width, height);
            UiPalette palette = context.Palette;
            using (context.Opacity(amount))
            {
                context.Fill(panel, palette.Footer);
                context.Border(panel, palette.StrongLine);
                context.Label(new Rect(panel.x + 10f, panel.y + 8f, Mathf.Max(0f, panel.width - 20f), Mathf.Max(0f, panel.height - 16f)), text, palette.FooterInk, TextAnchor.MiddleLeft, true);
            }
        }
    }
}
