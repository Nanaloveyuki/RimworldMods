using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiPageTransition
    {
        private readonly UiContext context;
        private UiTween opacity;
        private UiTween offset;
        private UiId active;
        private bool initialized;
        private bool switching;
        private int lastFrame = -1;
        private bool focusPending;
        private UiId entryFocus;

        public UiPageTransition(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public UiId ActivePage => active;
        public float Visibility => opacity.Value;
        public bool IsTransitioning => switching || opacity.IsRunning || offset.IsRunning;

        // The callback is caller-cached for the currently requested page. During the exit
        // interval NO page callback runs, so a removed destination is never evaluated again.
        // Each invocation runs zero or ONE callback; no old/new dual IMGUI evaluation.
        public void Draw(Rect rect, UiId page, Action<Rect> drawPage, bool visible = true,
            int direction = 1, UiId initialFocus = default(UiId))
        {
            if (drawPage == null) throw new ArgumentNullException(nameof(drawPage));
            direction = direction < 0 ? -1 : 1;
            if (!initialized)
            {
                active = page;
                offset.Snap(direction * 16f);
                initialized = true;
                entryFocus = initialFocus;
                focusPending = initialFocus.Value != 0;
            }
            switching = !active.Equals(page);
            float target = visible && !switching ? 1f : 0f;
            float alpha = opacity.Step(target, context.Time, target > 0f ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
            float x = offset.Step(target > 0f ? 0f : -direction * 12f, context.Time,
                target > 0f ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
            if (switching && alpha <= 0f && !opacity.IsRunning)
            {
                active = page;
                switching = false;
                offset.Snap(direction * 16f);
                x = offset.Value;
                alpha = opacity.Step(visible ? 1f : 0f, context.Time, UiMotion.Enter, context.ReducedMotion);
                entryFocus = initialFocus;
                focusPending = initialFocus.Value != 0;
            }
            // Cached frame is only for entry-focus handoff, never for skipping IMGUI passes.
            if (lastFrame != context.Frame && focusPending && alpha > 0f && visible && !switching && context.IsInputAllowed)
            {
                context.Focus(entryFocus);
                focusPending = false;
            }
            lastFrame = context.Frame;
            if (switching || (!visible && alpha <= 0f) || rect.width <= 0f || rect.height <= 0f) return;
            using (context.Input(visible && !switching))
            using (context.Opacity(alpha))
            using (context.Clip(rect))
            using (context.Clip(new Rect(x, 0f, rect.width, rect.height)))
                drawPage(new Rect(0f, 0f, rect.width, rect.height));
        }
    }
}
