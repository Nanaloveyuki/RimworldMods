using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiDialog
    {
        private readonly UiModalState modal;

        public UiDialog(UiContext context) { modal = new UiModalState(context); }
        public UiId Id => modal.Id;
        public bool IsVisible => modal.Visible;
        public float Progress => modal.Progress;

        // The caller must register all retained overlays before rendering background controls.
        public void Prepare(bool open) => modal.Prepare(open);

        // Scale is applied to actual panel geometry, not GUI.matrix: hit tests follow visuals.
        // Content keeps its natural layout and is clipped to the growing panel during entry.
        public bool Draw(Rect viewport, Vector2 size, string title, Action<Rect> drawContent,
            bool closeOnScrim = false, bool dismissible = true)
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (drawContent == null) throw new ArgumentNullException(nameof(drawContent));
            if (float.IsNaN(size.x) || float.IsInfinity(size.x) || size.x <= 0f
                || float.IsNaN(size.y) || float.IsInfinity(size.y) || size.y <= 0f)
                throw new ArgumentOutOfRangeException(nameof(size));
            if (!modal.Visible || viewport.width <= 0f || viewport.height <= 0f) return false;
            UiContext context = modal.Context;
            float naturalWidth = Mathf.Min(size.x, viewport.width);
            float naturalHeight = Mathf.Min(size.y, viewport.height);
            float scale = UiMotion.Lerp(0.94f, 1f, modal.Progress);
            float width = naturalWidth * scale, height = naturalHeight * scale;
            using (context.Layer(modal.Id))
            using (context.Clip(viewport))
            {
                Rect panel = new Rect((viewport.width - width) * 0.5f, (viewport.height - height) * 0.5f, width, height);
                bool close = modal.DrawChrome(new Rect(0f, 0f, viewport.width, viewport.height), panel, title, closeOnScrim, dismissible);
                Rect content = new Rect(panel.x + 20f, panel.y + 72f, Mathf.Max(0f, panel.width - 40f), Mathf.Max(0f, panel.height - 92f));
                Rect natural = new Rect(0f, 0f, Mathf.Max(0f, naturalWidth - 40f), Mathf.Max(0f, naturalHeight - 92f));
                using (context.Input(modal.Open))
                using (context.Opacity(modal.Progress))
                using (context.Clip(content))
                using (context.Clip(natural))
                    drawContent(natural);
                return close;
            }
        }
    }
}
