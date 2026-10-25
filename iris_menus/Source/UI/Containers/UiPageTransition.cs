using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiPageTransition
    {
        private readonly UiContext context;
        private UiTween opacity;
        private UiId active;
        private bool initialized;
        private int lastFrame = -1;
        private bool focusPending;
        private UiId entryFocus;

        public UiPageTransition(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public UiId ActivePage => active;
        public float Visibility => opacity.Value;
        public bool IsTransitioning => opacity.IsRunning;

        // Replace immediately; only the latest page callback runs, once per IMGUI pass.
        public void Draw(Rect rect, UiId page, Action<Rect> drawPage, bool visible = true,
            UiId initialFocus = default(UiId))
        {
            if (drawPage == null) throw new ArgumentNullException(nameof(drawPage));
            if (!initialized || !active.Equals(page))
            {
                active = page;
                opacity.Snap(0f);
                initialized = true;
                entryFocus = initialFocus;
                focusPending = initialFocus.Value != 0;
            }
            float alpha = opacity.Step(visible ? 1f : 0f, context.Time,
                visible ? UiMotion.Sheet : UiMotion.Exit, context.ReducedMotion, UiEase.Iris);
            // Cached frame is only for entry-focus handoff, never for skipping IMGUI passes.
            if (lastFrame != context.Frame && focusPending && alpha > 0f && visible && context.IsInputAllowed)
            {
                context.Focus(entryFocus);
                focusPending = false;
            }
            lastFrame = context.Frame;
            if ((!visible && alpha <= 0f) || rect.width <= 0f || rect.height <= 0f) return;
            using (context.Input(visible))
            using (context.Opacity(alpha))
            using (context.Clip(rect))
                drawPage(new Rect(0f, 0f, rect.width, rect.height));
        }
    }
}
