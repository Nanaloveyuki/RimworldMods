using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public enum UiDrawerSide { Left, Right }

    public sealed class UiDrawer
    {
        private readonly UiModalState modal;

        public UiDrawer(UiContext context) { modal = new UiModalState(context, true); }
        public UiId Id => modal.Id;
        public bool IsVisible => modal.Visible;
        public float Progress => modal.Position;

        // Register before background drawing. Prepare stacked overlays in back-to-front order.
        public void Prepare(bool open) => modal.Prepare(open);

        // Returns a close request only; the caller owns the open value and unsaved work policy.
        // Invoke while IsVisible, including the exit; content receives natural LOCAL coordinates.
        public bool Draw(Rect viewport, float width, string title, Action<Rect> drawContent,
            UiDrawerSide side = UiDrawerSide.Right, bool closeOnScrim = true, bool dismissible = true)
        {
            if (title == null) throw new ArgumentNullException(nameof(title));
            if (drawContent == null) throw new ArgumentNullException(nameof(drawContent));
            if (float.IsNaN(width) || float.IsInfinity(width) || width <= 0f) throw new ArgumentOutOfRangeException(nameof(width));
            if (side != UiDrawerSide.Left && side != UiDrawerSide.Right) throw new ArgumentOutOfRangeException(nameof(side));
            if (!modal.Visible || viewport.width <= 0f || viewport.height <= 0f) return false;
            UiContext context = modal.Context;
            width = Mathf.Min(width, viewport.width);
            float x = side == UiDrawerSide.Right
                ? viewport.xMax - width * modal.Position
                : viewport.x - width * (1f - modal.Position);
            Rect panel = new Rect(x, viewport.y, width, viewport.height);
            using (context.Layer(modal.Id))
            using (context.Clip(viewport))
            {
                panel.x -= viewport.x;
                panel.y -= viewport.y;
                Rect localViewport = new Rect(0f, 0f, viewport.width, viewport.height);
                bool close = modal.DrawChrome(localViewport, panel, title, closeOnScrim, dismissible);
                Rect content = new Rect(panel.x + 16f, panel.y + 64f, Mathf.Max(0f, width - 32f), Mathf.Max(0f, panel.height - 80f));
                using (context.Input(modal.Open))
                using (context.Clip(content))
                    drawContent(new Rect(0f, 0f, content.width, content.height));
                return close;
            }
        }
    }
}
