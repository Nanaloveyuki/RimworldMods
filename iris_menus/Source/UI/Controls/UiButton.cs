using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiButton
    {
        private readonly UiContext context;
        private UiControlVisual visual;

        public UiButton(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
        }

        public UiId Id { get; }

        public bool Draw(Rect rect, string label, bool enabled = true, bool primary = false)
        {
            UiInteraction interaction = context.Interact(Id, rect, enabled);
            visual.Step(context, interaction, enabled);
            UiPalette palette = context.Palette;
            using (context.Opacity(visual.Opacity))
            {
                Color normal = primary ? palette.Selected : palette.Raised;
                Color background = visual.Background(palette, normal);
                if (primary) background = Color.Lerp(normal, palette.Signal, visual.Hover * 0.15f + visual.Press * 0.2f);
                context.Fill(rect, background);
                context.Border(rect, Color.Lerp(palette.Line, palette.StrongLine, visual.Hover));
                Rect textRect = new Rect(rect.x + 10f, rect.y + visual.Press, Mathf.Max(0f, rect.width - 20f), rect.height);
                context.Label(textRect, label ?? string.Empty, primary ? palette.OnSelected : palette.Ink, TextAnchor.MiddleCenter);
                visual.DrawFocus(context, rect);
            }
            return interaction.Clicked;
        }
    }
}
