using System;
using UnityEngine;

namespace IrisMenus.UI
{
    internal sealed class UiModalState
    {
        internal readonly UiContext Context;
        internal readonly UiId Id;
        private readonly UiId closeId;
        private UiId previousFocus;
        private bool previousFocusVisible;
        private readonly bool slides;
        private UiTween opacity, position;
        private UiTween hover, press, focus;
        private bool retained;
        private bool focusOnEntry;
        private bool wasTopLayer;

        internal UiModalState(UiContext context, bool slides = false)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            this.slides = slides;
            Id = context.CreateId();
            closeId = context.CreateId();
        }

        internal bool Open { get; private set; }
        internal bool Visible => retained;
        internal float Progress => opacity.Value;
        internal float Position => position.Value;
        internal float PanelOpacity => slides ? 1f : Progress;

        // Call low-to-high for every modal, every IMGUI pass, before background controls.
        internal void Prepare(bool open)
        {
            if (open && !retained)
            {
                previousFocus = Context.Focused;
                previousFocusVisible = Context.FocusVisible;
                focusOnEntry = true;
                retained = true;
                position.Snap(0f);
            }
            Open = open;
            float alpha = opacity.Step(open ? 1f : 0f, Context.Time,
                UiMotion.Fast, Context.ReducedMotion);
            if (slides || open)
                position.Step(open ? 1f : 0f, Context.Time,
                    UiMotion.Enter, Context.ReducedMotion, UiEase.Exit);
            else position.Snap(position.Value);
            if (!open && alpha <= 0f && !opacity.IsRunning && (!slides || !position.IsRunning))
            {
                if (retained && wasTopLayer) Context.RestoreFocus(previousFocus, previousFocusVisible);
                retained = false;
                focusOnEntry = false;
            }
            if (retained) Context.SetModal(Id);
        }

        internal bool DrawChrome(Rect viewport, Rect panel, string title, bool closeOnScrim, bool dismissible)
        {
            wasTopLayer = Context.IsInputAllowed;
            bool closeRequested = false;
            UiPalette palette = Context.Palette;
            using (Context.Opacity(Progress)) Context.Fill(viewport, palette.Scrim);
            if (Context.IsInputAllowed)
            {
                if (Context.ConsumeKey(KeyCode.Escape)) closeRequested = Open && dismissible;
                Event input = Context.Event;
                if (input != null && input.type == EventType.MouseDown && input.button == 0
                    && Context.Hit(viewport) && !panel.Contains(input.mousePosition))
                {
                    Context.Consume();
                    if (Open && dismissible && closeOnScrim) closeRequested = true;
                }
            }
            using (Context.Opacity(PanelOpacity))
            {
                DrawShadow(panel);
                Context.Fill(panel, palette.Overlay);
                float headerHeight = slides ? 48f : 64f;
                Rect header = new Rect(panel.x, panel.y, panel.width, Mathf.Min(headerHeight, panel.height));
                DrawHeader(header, palette);
                if (!slides) DrawDots(new Rect(panel.x, header.yMax, panel.width, Mathf.Max(0f, panel.height - header.height)), palette);
                Rect close = new Rect(panel.xMax - 44f, panel.y + (headerHeight - 40f) * 0.5f, 40f, 40f);
                using (Context.Input(Open && dismissible))
                {
                    if (focusOnEntry && dismissible && Context.IsInputAllowed)
                    {
                        Context.Focus(closeId);
                        focusOnEntry = false;
                    }
                    UiInteraction interaction = Context.Interact(closeId, close, dismissible);
                    if (interaction.Clicked) closeRequested = true;
                    float h = hover.Step(interaction.Hovered ? 1f : 0f, Context.Time, UiMotion.Enter, Context.ReducedMotion);
                    float p = press.Step(interaction.Pressed ? 1f : 0f, Context.Time, UiMotion.Fast, Context.ReducedMotion);
                    float f = focus.Step(interaction.Focused && Context.FocusVisible ? 1f : 0f, Context.Time, UiMotion.Fast, Context.ReducedMotion);
                    if (dismissible)
                    {
                        Color outline = palette.HeaderInk;
                        outline.a *= f;
                        Context.Border(new Rect(close.x + 2f, close.y + 2f, close.width - 4f, close.height - 4f), outline, 2f);
                        Color ink = Color.Lerp(palette.HeaderInk, new Color(0.831f, 0.831f, 0.831f), p);
                        UiGlyph.Draw(Context, new Rect(close.x + 8f, close.y + 8f, 24f, 24f), UiGlyphKind.Close, ink, h * 90f);
                    }
                }
                float previousSize = Context.FontSize;
                FontStyle previousStyle = Context.FontStyle;
                try
                {
                    Context.FontSize = slides ? 18f : 20f;
                    Context.FontStyle = FontStyle.Normal;
                    Context.Label(new Rect(panel.x + (slides ? 16f : 48f), panel.y, Mathf.Max(0f, panel.width - (slides ? 64f : 96f)), headerHeight),
                        title, palette.HeaderInk, slides ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter, true);
                }
                finally { Context.FontSize = previousSize; Context.FontStyle = previousStyle; }
            }
            return closeRequested;
        }

        private void DrawHeader(Rect header, UiPalette palette)
        {
            Context.Fill(header, palette.Header);
            if (!Context.IsRepaint) return;
            Color hatch = palette.HeaderInk;
            hatch.a *= 0.05f;
            using (Context.Clip(header))
            {
                for (float x = -header.height; x < header.width; x += 6f)
                    Context.Line(new Vector2(x, 0f), new Vector2(x + header.height, header.height), hatch, 1.9f);
                Color line = palette.HeaderInk;
                line.a *= 0.13f;
                Context.Fill(new Rect(0f, header.height - 1f, header.width, 1f), line);
            }
        }

        private void DrawDots(Rect body, UiPalette palette)
        {
            if (!Context.IsRepaint) return;
            Color dot = palette.Ink;
            dot.a *= 0.1f;
            for (float y = 8f; y < body.height; y += 16f)
            {
                bool edgeRow = y < 16f || y >= body.height - 16f;
                for (float x = 8f; x < body.width; x += 16f)
                {
                    if (!edgeRow && x >= 16f && x < body.width - 16f) continue;
                    Context.Fill(new Rect(body.x + x - 1f, body.y + y - 1f, 2f, 2f), dot, 1f);
                }
            }
        }

        private void DrawShadow(Rect panel)
        {
            if (!Context.IsRepaint) return;
            // Layered, non-offset rings approximate the library's 24px ambient shadow.
            for (int spread = 24; spread >= 2; spread -= 2)
            {
                float distance = spread / 24f;
                float alpha = 0.12f * (1f - distance) * (1f - distance);
                Context.Border(new Rect(panel.x - spread, panel.y - spread, panel.width + spread * 2f, panel.height + spread * 2f),
                    new Color(0f, 0f, 0f, alpha), 2f);
            }
        }
    }
}
