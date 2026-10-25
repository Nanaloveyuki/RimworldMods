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

        // Geometry stays at its natural size throughout; entry is an 8px rise plus opacity.
        // Exit fades in place, preserving the callback's layout and stable control identities.
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
            float offset = 8f * (1f - modal.Position);
            using (context.Layer(modal.Id))
            using (context.Clip(viewport))
            {
                Rect panel = new Rect((viewport.width - naturalWidth) * 0.5f,
                    (viewport.height - naturalHeight) * 0.5f + offset, naturalWidth, naturalHeight);
                bool close = modal.DrawChrome(new Rect(0f, 0f, viewport.width, viewport.height), panel, title, closeOnScrim, dismissible);
                Rect content = new Rect(panel.x + 36f, panel.y + 100f,
                    Mathf.Max(0f, naturalWidth - 72f), Mathf.Max(0f, naturalHeight - 136f));
                using (context.Input(modal.Open))
                using (context.Opacity(modal.PanelOpacity))
                using (context.Clip(content))
                    drawContent(new Rect(0f, 0f, content.width, content.height));
                return close;
            }
        }
    }
}
