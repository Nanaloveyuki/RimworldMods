using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiFooter
    {
        private readonly UiContext context;
        private UiTween visibility;

        public UiFooter(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public float Visibility => visibility.Value;

        // Footer is intentionally dark in BOTH themes; use FooterInk for custom actions.
        public void Draw(Rect rect, string leading, string trailing = null, Action<Rect> drawActions = null,
            float actionsWidth = 0f, bool visible = true)
        {
            if (leading == null) throw new ArgumentNullException(nameof(leading));
            if (float.IsNaN(actionsWidth) || float.IsInfinity(actionsWidth) || actionsWidth < 0f)
                throw new ArgumentOutOfRangeException(nameof(actionsWidth));
            float alpha = visibility.Step(visible ? 1f : 0f, context.Time,
                visible ? UiMotion.Enter : UiMotion.Exit, context.ReducedMotion);
            if ((!visible && alpha <= 0f) || rect.width <= 0f || rect.height <= 0f) return;
            using (context.Input(visible))
            using (context.Opacity(alpha))
            using (context.Clip(rect))
            {
                UiPalette palette = context.Palette;
                context.Fill(new Rect(0f, 0f, rect.width, rect.height), palette.Footer);
                float actionWidth = drawActions == null ? 0f : Mathf.Min(actionsWidth, Mathf.Max(0f, rect.width - 32f));
                float textWidth = Mathf.Max(0f, rect.width - actionWidth - 32f);
                float leftWidth = trailing == null ? textWidth : textWidth * 0.6f;
                context.Label(new Rect(16f, 0f, leftWidth, rect.height), leading, palette.FooterInk);
                if (trailing != null)
                {
                    Color muted = palette.FooterInk;
                    muted.a *= 0.7f;
                    context.Label(new Rect(16f + leftWidth, 0f, textWidth - leftWidth, rect.height), trailing, muted, TextAnchor.MiddleRight);
                }
                if (drawActions != null)
                {
                    Rect actions = new Rect(rect.width - actionWidth - 16f, 0f, actionWidth, rect.height);
                    using (context.Clip(actions)) drawActions(new Rect(0f, 0f, actions.width, actions.height));
                }
            }
        }
    }
}
