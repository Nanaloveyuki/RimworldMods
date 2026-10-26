using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public enum UiToastTone { Neutral, Info, Success, Warning, Danger }

    /// <summary>A nonmodal, bottom-centered notification; one retained message per surface.</summary>
    public sealed class UiToast
    {
        public const float MinimumDurationSeconds = 1f;
        public const float MaximumDurationSeconds = 10f;

        private static readonly Color Background = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color ProgressTrack = new Color(1f, 1f, 1f, 0.15f);
        private static readonly Color InfoInk = new Color(45f / 255f, 143f / 255f, 194f / 255f);
        private static readonly Color SuccessInk = new Color(0f, 216f / 255f, 114f / 255f);
        private static readonly Color WarningInk = new Color(252f / 255f, 108f / 255f, 0f);
        private static readonly Color DangerInk = new Color(247f / 255f, 64f / 255f, 64f / 255f);
        private static readonly Vector2[] Circle = CreateCircle();
        private readonly UiContext context;
        private readonly UiButton close;
        private UiTween opacity, entrance;
        private string message;
        private UiToastTone tone;
        private bool open, restartTimer;
        private double started;
        private float duration, defaultDuration = 3f;
        private string measuredMessage;
        private UiToastTone measuredTone;
        private float measuredWidth = -1f, measuredHeight, measuredScale, measuredFont;
        private Vector2 size;

        public UiToast(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            close = new UiButton(context);
        }

        public float DefaultDurationSeconds
        {
            get => defaultDuration;
            set => defaultDuration = ClampDuration(value);
        }

        internal bool BlocksPointer { get; private set; }


        /// <summary>Replaces the previous message; an identical active message only refreshes its timer.</summary>
        public void Show(string message, UiToastTone tone = UiToastTone.Neutral, float? durationSeconds = null)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (string.IsNullOrWhiteSpace(message)) { Dismiss(); return; }
            float seconds = ClampDuration(durationSeconds ?? defaultDuration);
            bool same = open && this.message == message && this.tone == tone
                && (restartTimer || context.Time - started < duration);
            this.message = message;
            this.tone = tone;
            duration = seconds;
            open = restartTimer = true;
            if (!same)
            {
                opacity.Snap(0f);
                entrance.Snap(0f);
            }
        }

        public void Dismiss()
        {
            open = restartTimer = false;
            context.Release(close.Id);
            if (context.IsFocused(close.Id)) context.Focus(default);
        }

        // Call once per IMGUI pass in viewport coordinates, after BeginFrame and before EndFrame.
        // For frontmost hit testing, draw before content on non-Repaint passes, after it on Repaint.
        // This never registers a modal or takes keyboard focus when a notification appears.
        public void Draw(Rect viewport, float scale = 1f)
        {
            BlocksPointer = false;
            if (message == null) return;
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f)
                throw new ArgumentOutOfRangeException(nameof(scale));
            if (restartTimer)
            {
                if (!context.IsRepaint) return;
                started = context.Time;
                restartTimer = false;
            }
            float progress = Mathf.Clamp01((float)((context.Time - started) / duration));
            if (open && progress >= 1f) Dismiss();
            float alpha = opacity.Step(open ? 1f : 0f, context.Time, UiMotion.Fast, context.ReducedMotion);
            float entry = open
                ? entrance.Step(1f, context.Time, UiMotion.Enter, context.ReducedMotion, UiEase.Exit)
                : entrance.Value;
            if (!open && alpha <= 0f)
            {
                message = null;
                return;
            }
            if (viewport.width <= 0f || viewport.height <= 0f) return;

            float oldFont = context.FontSize;
            FontStyle oldStyle = context.FontStyle;
            context.FontSize = 16f * scale;
            context.FontStyle = FontStyle.Normal;
            try
            {
                float margin = Mathf.Min(24f * scale, Mathf.Min(viewport.width, viewport.height) * 0.05f);
                float availableWidth = Mathf.Max(0f, viewport.width - margin * 2f);
                float availableHeight = Mathf.Max(0f, viewport.height - margin * 2f);
                Measure(availableWidth, availableHeight, scale);
                if (size.x <= 0f || size.y <= 0f) return;
                Rect panel = new Rect(viewport.center.x - size.x * 0.5f,
                    viewport.yMax - margin - size.y + (1f - entry) * 12f * scale, size.x, size.y);
                Rect closeRect = new Rect(panel.xMax - 40f * scale,
                    panel.center.y - 16f * scale, 32f * scale, 32f * scale);
                Event input = context.Event;
                BlocksPointer = input != null && (context.Hit(panel) || context.IsCaptured(close.Id))
                    && (input.type == EventType.MouseDown || input.type == EventType.MouseUp
                        || input.type == EventType.MouseDrag || input.type == EventType.ScrollWheel);
                using (context.Opacity(alpha))
                {
                    DrawShadow(panel, scale);
                    context.Fill(panel, Background);
                    float left = panel.x + 20f * scale;
                    if (tone != UiToastTone.Neutral)
                    {
                        DrawGlyph(new Rect(left, panel.center.y - 8f * scale, 16f * scale, 16f * scale), ToneInk());
                        left += 28f * scale;
                    }
                    using (context.Clip(new Rect(left, panel.y + 10f * scale,
                        Mathf.Max(0f, closeRect.x - 12f * scale - left), Mathf.Max(0f, panel.height - 20f * scale))))
                        context.Label(new Rect(0f, 0f, Mathf.Max(0f, closeRect.x - 12f * scale - left),
                            Mathf.Max(0f, panel.height - 20f * scale)), message, Color.white, TextAnchor.MiddleLeft, true);

                    UiInteraction interaction = context.Interact(close.Id, closeRect, open);
                    close.DrawVisual(closeRect, string.Empty, interaction, open,
                        variant: UiButtonVariant.Control, scale: scale, drawContent: false);
                    UiGlyph.Draw(context, new Rect(closeRect.x + 8f * scale, closeRect.y + 8f * scale,
                        16f * scale, 16f * scale), UiGlyphKind.Close, Color.white);
                    if (interaction.Clicked) Dismiss();

                    Rect track = new Rect(panel.x, panel.yMax - 2f * scale, panel.width, 2f * scale);
                    context.Fill(track, ProgressTrack);
                    track.width *= progress;
                    context.Fill(track, ToneInk());
                }
                // The toast body is not a focusable control. Only pointer events inside it are absorbed.
                Event evt = context.Event;
                if (evt != null && context.Hit(panel)
                    && (evt.type == EventType.MouseDown || evt.type == EventType.MouseUp
                        || evt.type == EventType.MouseDrag || evt.type == EventType.ScrollWheel))
                    context.Consume();
            }
            finally
            {
                context.FontSize = oldFont;
                context.FontStyle = oldStyle;
            }
        }

        private void Measure(float width, float height, float scale)
        {
            if (measuredMessage == message && measuredTone == tone && measuredWidth == width
                && measuredHeight == height && measuredScale == scale && measuredFont == context.FontSize) return;
            measuredMessage = message;
            measuredTone = tone;
            measuredWidth = width;
            measuredHeight = height;
            measuredScale = scale;
            measuredFont = context.FontSize;
            float chrome = (tone == UiToastTone.Neutral ? 72f : 100f) * scale;
            size.x = Mathf.Min(width, Mathf.Min(600f * scale,
                Mathf.Max(chrome + 40f * scale, UiTextMetrics.Width(context, message) + chrome)));
            float textHeight = UiTextMetrics.Height(context, message, Mathf.Max(1f, size.x - chrome));
            size.y = Mathf.Min(height, Mathf.Max(52f * scale, textHeight + 20f * scale));
        }

        private Color ToneInk()
        {
            switch (tone)
            {
                case UiToastTone.Info: return InfoInk;
                case UiToastTone.Success: return SuccessInk;
                case UiToastTone.Warning: return WarningInk;
                case UiToastTone.Danger: return DangerInk;
                default: return Color.white;
            }
        }

        private void DrawGlyph(Rect rect, Color ink)
        {
            if (!context.IsRepaint) return;
            float stroke = rect.width / 16f * 1.25f;
            switch (tone)
            {
                case UiToastTone.Info:
                    context.Border(new Rect(rect.x + rect.width / 6f, rect.y + rect.height / 6f,
                        rect.width * 2f / 3f, rect.height * 2f / 3f), ink, stroke);
                    GlyphLine(rect, 12f, 11f, 12f, 17f, ink, stroke);
                    GlyphLine(rect, 12f, 7f, 12f, 9f, ink, stroke);
                    break;
                case UiToastTone.Success:
                    for (int i = 1; i < Circle.Length; i++)
                        GlyphLine(rect, Circle[i - 1].x, Circle[i - 1].y, Circle[i].x, Circle[i].y, ink, stroke);
                    GlyphLine(rect, 7.5f, 12.5f, 10.5f, 15.5f, ink, stroke);
                    GlyphLine(rect, 10.5f, 15.5f, 16.5f, 8.5f, ink, stroke);
                    break;
                case UiToastTone.Warning:
                    GlyphLine(rect, 12f, 4f, 21f, 20f, ink, stroke);
                    GlyphLine(rect, 21f, 20f, 3f, 20f, ink, stroke);
                    GlyphLine(rect, 3f, 20f, 12f, 4f, ink, stroke);
                    GlyphLine(rect, 12f, 10f, 12f, 15f, ink, stroke);
                    GlyphLine(rect, 12f, 16.5f, 12f, 18f, ink, stroke);
                    break;
                case UiToastTone.Danger:
                    GlyphLine(rect, 12f, 2.5f, 21.5f, 12f, ink, stroke);
                    GlyphLine(rect, 21.5f, 12f, 12f, 21.5f, ink, stroke);
                    GlyphLine(rect, 12f, 21.5f, 2.5f, 12f, ink, stroke);
                    GlyphLine(rect, 2.5f, 12f, 12f, 2.5f, ink, stroke);
                    GlyphLine(rect, 12f, 7f, 12f, 13f, ink, stroke);
                    GlyphLine(rect, 12f, 15f, 12f, 17f, ink, stroke);
                    break;
            }
        }

        private void GlyphLine(Rect rect, float x1, float y1, float x2, float y2, Color ink, float stroke)
        {
            context.Line(new Vector2(rect.x + rect.width * x1 / 24f, rect.y + rect.height * y1 / 24f),
                new Vector2(rect.x + rect.width * x2 / 24f, rect.y + rect.height * y2 / 24f), ink, stroke);
        }

        private void DrawShadow(Rect panel, float scale)
        {
            if (!context.IsRepaint) return;
            for (int spread = 6; spread >= 2; spread -= 2)
            {
                float distance = spread * scale;
                context.Border(new Rect(panel.x - distance, panel.y - distance,
                    panel.width + distance * 2f, panel.height + distance * 2f),
                    new Color(0f, 0f, 0f, 0.035f), 2f * scale);
            }
        }

        private static float ClampDuration(float seconds)
        {
            if (float.IsNaN(seconds) || float.IsInfinity(seconds))
                throw new ArgumentOutOfRangeException(nameof(seconds));
            return Mathf.Clamp(seconds, MinimumDurationSeconds, MaximumDurationSeconds);
        }

        private static Vector2[] CreateCircle()
        {
            var points = new Vector2[25];
            for (int i = 0; i < points.Length; i++)
            {
                double radians = Math.PI * 2d * i / (points.Length - 1);
                points[i] = new Vector2(12f + 9f * (float)Math.Cos(radians), 12f + 9f * (float)Math.Sin(radians));
            }
            return points;
        }
    }
}
