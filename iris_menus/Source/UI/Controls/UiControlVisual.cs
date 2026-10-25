using UnityEngine;

namespace IrisMenus.UI
{
    internal struct UiControlVisual
    {
        private UiTween hover;
        private UiTween press;
        private UiTween focus;
        private UiTween disabled;
        private bool initialized;

        internal float Hover => hover.Value;
        internal float Press => press.Value;
        internal float Focus => focus.Value;
        internal float Opacity => UiMotion.Lerp(1f, 0.42f, disabled.Value);

        internal void Step(UiContext context, UiInteraction interaction, bool enabled)
        {
            float h = enabled && interaction.Hovered ? 1f : 0f;
            float p = enabled && interaction.Pressed ? 1f : 0f;
            float f = enabled && interaction.Focused && context.FocusVisible ? 1f : 0f;
            float d = enabled ? 0f : 1f;
            if (!initialized)
            {
                hover.Snap(h);
                press.Snap(p);
                focus.Snap(f);
                disabled.Snap(d);
                initialized = true;
            }
            hover.Step(h, context.Time, UiMotion.Fast, context.ReducedMotion);
            press.Step(p, context.Time, UiMotion.Fast, context.ReducedMotion);
            focus.Step(f, context.Time, UiMotion.Fast, context.ReducedMotion);
            disabled.Step(d, context.Time, UiMotion.Standard, context.ReducedMotion);
        }

        internal Color Background(UiPalette palette, Color normal)
        {
            Color tint = palette.Hover;
            Color color = Color.Lerp(normal, new Color(tint.r, tint.g, tint.b, normal.a), Hover * tint.a);
            return Color.Lerp(color, palette.Selected, Press * 0.6f);
        }

        internal void DrawFocus(UiContext context, Rect rect)
        {
            if (Focus <= 0f) return;
            Color color = context.Palette.Signal;
            color.a *= Focus;
            context.Border(rect, color, 2f);
        }
    }

    internal struct UiControlLine
    {
        private UiTween focus;

        internal void Draw(UiContext context, Rect rect, bool focused, float hover, float invalid = 0f, float scale = 1f)
        {
            float amount = focus.Step(focused && invalid <= 0f ? 1f : 0f, context.Time,
                focused ? UiMotion.Enter : UiMotion.Fast, context.ReducedMotion, focused ? UiEase.Exit : UiEase.Standard);
            float height = Mathf.Min(rect.height, 2f * Mathf.Max(0f, scale));
            Rect line = new Rect(rect.x, rect.yMax - height, rect.width, height);
            Color border = Color.Lerp(context.Palette.StrongLine, context.Palette.Muted, hover);
            context.Fill(line, Color.Lerp(border, context.Palette.Error, invalid));
            line.width *= amount;
            context.Fill(line, context.Palette.Ink);
        }
    }
}
