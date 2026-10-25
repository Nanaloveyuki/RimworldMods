using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiTooltip
    {
        private readonly UiContext context;
        private sealed class ProviderState
        {
            public double WarmUntil = double.NegativeInfinity;
        }

        private static readonly ConditionalWeakTable<UiContext, ProviderState> providers = new ConditionalWeakTable<UiContext, ProviderState>();
        private readonly ProviderState provider;
        private float delay;
        private UiTween visibility;
        private UiTween entrance;
        private Rect anchorScreen;
        private string text = string.Empty;
        private bool eligible;
        private bool suppressed;
        private bool requested;
        private double enteredAt;
        private int observedFrame = -1;

        public UiTooltip(UiContext context, float delay = 0.3f)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            provider = providers.GetValue(context, CreateProvider);
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

        private static ProviderState CreateProvider(UiContext _) => new ProviderState();

        // Observe after the target control; targetId enables the same tooltip from keyboard focus.
        // This is not a second Interact call and does not participate in focus or mouse capture.
        public void Observe(Rect anchor, string content, UiId targetId = default(UiId), bool enabled = true)
        {
            observedFrame = context.Frame;
            bool keyboard = enabled && context.FocusVisible && !targetId.Equals(default(UiId)) && context.IsFocused(targetId);
            bool over = enabled && !string.IsNullOrEmpty(content) && context.IsInputAllowed && (context.Hit(anchor) || keyboard);
            if (!over)
            {
                eligible = false;
                suppressed = false;
                entrance.Snap(entrance.Value);
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
            bool instant = keyboard || context.Time <= provider.WarmUntil;
            bool next = !suppressed && (instant || visibility.Value > 0f || context.Time - enteredAt >= delay);
            if (next && !requested && visibility.Value <= 0f)
            {
                entrance.Snap(instant ? 1f : 0f);
                if (instant) visibility.Snap(1f);
            }
            if (next && keyboard) { entrance.Snap(1f); visibility.Snap(1f); }
            requested = next;
            if (requested) provider.WarmUntil = context.Time + 0.4;
            else entrance.Snap(entrance.Value);
        }

        // Keeps the exiting text/anchor while fading, even when the target is no longer drawn.
        public void Dismiss()
        {
            requested = false;
            entrance.Snap(entrance.Value);
            suppressed = true;
        }

        // Draw after all content, outside its scroll/clip groups. Size is explicit to keep
        // measurement and layout caller-owned; long text wraps inside the constrained rect.
        public void DrawOverlay(Rect bounds, Vector2 size, float scale = 1f)
        {
            if (observedFrame != context.Frame || !context.IsInputAllowed)
            {
                requested = false;
                eligible = false;
                suppressed = false;
                entrance.Snap(entrance.Value);
            }
            float entry = requested ? entrance.Step(1f, context.Time, UiMotion.Fast, context.ReducedMotion) : entrance.Value;
            float amount = visibility.Step(requested ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
            if (amount <= 0f || string.IsNullOrEmpty(text) || bounds.width <= 0f || bounds.height <= 0f) return;
            float unit = Mathf.Max(0f, scale);
            float width = Mathf.Clamp(size.x, 0f, bounds.width);
            float height = Mathf.Clamp(size.y, 0f, bounds.height);
            Vector2 local = GUIUtility.ScreenToGUIPoint(anchorScreen.position);
            Rect anchor = new Rect(local.x, local.y, anchorScreen.width, anchorScreen.height);
            bool below = anchor.y - height - 8f * unit < bounds.y && anchor.yMax + height + 8f * unit <= bounds.yMax;
            float x = Mathf.Clamp(anchor.center.x - width * 0.5f, bounds.x, bounds.xMax - width);
            float y = below ? anchor.yMax + 8f * unit : anchor.y - height - 8f * unit;
            y = Mathf.Clamp(y + (1f - entry) * (below ? -4f : 4f) * unit, bounds.y, bounds.yMax - height);
            Rect panel = new Rect(x, y, width, height);
            Color background = Color.Lerp(new Color(31f / 255f, 31f / 255f, 31f / 255f), new Color(250f / 255f, 250f / 255f, 250f / 255f), context.ThemeAmount);
            Color ink = Color.Lerp(new Color(250f / 255f, 250f / 255f, 250f / 255f), new Color(25f / 255f, 25f / 255f, 25f / 255f), context.ThemeAmount);
            using (context.Opacity(amount))
            {
                context.Fill(panel, background);
                float arrowX = Mathf.Clamp(anchor.center.x, panel.x + Mathf.Min(8f * unit, panel.width * 0.5f), panel.xMax - Mathf.Min(8f * unit, panel.width * 0.5f));
                float arrowY = below ? panel.y : panel.yMax;
                context.Triangle(new Vector2(arrowX - 5.66f * unit, arrowY), new Vector2(arrowX + 5.66f * unit, arrowY),
                    new Vector2(arrowX, arrowY + (below ? -5.66f : 5.66f) * unit), background);
                context.Label(new Rect(panel.x + 10f * unit, panel.y + 6f * unit, Mathf.Max(0f, panel.width - 20f * unit), Mathf.Max(0f, panel.height - 12f * unit)), text, ink, TextAnchor.MiddleLeft, true);
            }
        }
    }
}
