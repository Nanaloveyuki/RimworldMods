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
        private UiTween progress;
        private UiTween hover, press, focus;
        private bool retained;
        private bool focusOnEntry;
        private bool wasTopLayer;

        internal UiModalState(UiContext context)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
            closeId = context.CreateId();
        }

        internal bool Open { get; private set; }
        internal bool Visible => retained;
        internal float Progress => progress.Value;

        // Call low-to-high for every modal, every IMGUI pass, before background controls.
        internal void Prepare(bool open)
        {
            if (open && !retained)
            {
                previousFocus = Context.Focused;
                focusOnEntry = true;
                retained = true;
            }
            Open = open;
            float value = progress.Step(open ? 1f : 0f, Context.Time,
                open ? UiMotion.Enter : UiMotion.Exit, Context.ReducedMotion);
            if (!open && value <= 0f && !progress.IsRunning)
            {
                if (retained && wasTopLayer) Context.Focus(previousFocus);
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
            using (Context.Opacity(Progress))
            {
                Context.Fill(panel, palette.Surface);
                Context.Border(panel, palette.StrongLine);
                Rect close = new Rect(panel.xMax - 48f, panel.y + 12f, 32f, 32f);
                using (Context.Input(Open && dismissible))
                {
                    if (focusOnEntry && dismissible && Context.IsInputAllowed)
                    {
                        Context.Focus(closeId);
                        focusOnEntry = false;
                    }
                    UiInteraction interaction = Context.Interact(closeId, close, dismissible);
                    if (interaction.Clicked) closeRequested = true;
                    float h = hover.Step(interaction.Hovered ? 1f : 0f, Context.Time, UiMotion.Fast, Context.ReducedMotion);
                    float p = press.Step(interaction.Pressed ? 1f : 0f, Context.Time, UiMotion.Fast, Context.ReducedMotion);
                    float f = focus.Step(interaction.Focused && Context.FocusVisible ? 1f : 0f, Context.Time, UiMotion.Fast, Context.ReducedMotion);
                    if (dismissible)
                    {
                        Color background = palette.Hover;
                        background.a *= Mathf.Max(h, p);
                        Context.Fill(close, background);
                        Color outline = palette.Signal;
                        outline.a *= f;
                        Context.Border(close, outline);
                        Context.Line(new Vector2(close.x + 11f, close.y + 11f), new Vector2(close.x + 21f, close.y + 21f), palette.Ink, 1.5f);
                        Context.Line(new Vector2(close.x + 21f, close.y + 11f), new Vector2(close.x + 11f, close.y + 21f), palette.Ink, 1.5f);
                    }
                }
                Context.Label(new Rect(panel.x + 20f, panel.y + 12f, Mathf.Max(0f, panel.width - 80f), 32f), title, palette.Ink);
                Context.Fill(new Rect(panel.x + 1f, panel.y + 55f, Mathf.Max(0f, panel.width - 2f), 1f), palette.Line);
            }
            return closeRequested;
        }
    }
}
