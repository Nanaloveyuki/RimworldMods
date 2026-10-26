using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiSegments
    {
        private readonly UiContext context;
        private string[] options;
        private UiId[] ids;
        private UiControlVisual[] visuals;
        private UiTween selectedPosition;
        private UiControlLine line;
        private bool initialized;

        public UiSegments(UiContext context, string[] options)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            SetOptions(options);
        }

        public int Count => options.Length;
        public UiId GetItemId(int index) => ids[index];

        // Configuration is deliberately separate from drawing; the caller retains the labels array.
        public void SetOptions(string[] labels)
        {
            if (labels == null) throw new ArgumentNullException(nameof(labels));
            if (labels.Length == 0) throw new ArgumentException("At least one segment is required.", nameof(labels));
            for (int i = 0; i < labels.Length; i++)
                if (labels[i] == null) throw new ArgumentException("Segment labels cannot be null.", nameof(labels));
            if (ids == null || ids.Length != labels.Length)
            {
                if (ids != null)
                    for (int i = 0; i < ids.Length; i++) context.Release(ids[i]);
                ids = new UiId[labels.Length];
                visuals = new UiControlVisual[labels.Length];
                for (int i = 0; i < ids.Length; i++) ids[i] = context.CreateId();
                initialized = false;
            }
            options = labels;
        }

        public int Draw(Rect rect, int selected, bool enabled = true, float scale = 1f)
        {
            selected = Mathf.Clamp(selected, 0, options.Length - 1);
            int previous = selected;
            float inset = Mathf.Min(2f * scale, Mathf.Max(0f, Mathf.Min(rect.width, rect.height) * 0.25f));
            float width = Mathf.Max(0f, rect.width - inset * 2f) / options.Length;
            float height = Mathf.Max(0f, rect.height - inset * 2f - 2f * scale);
            bool focused = false;
            float hover = 0f;
            UiPalette palette = context.Palette;
            context.Fill(rect, palette.Input);
            for (int i = 0; i < options.Length; i++)
            {
                Rect item = new Rect(rect.x + inset + width * i, rect.y + inset, width, height);
                UiInteraction interaction = context.Interact(ids[i], item, enabled);
                if (interaction.Clicked) selected = i;
                if (enabled && interaction.Focused && context.IsInputAllowed)
                {
                    int next = i;
                    if (context.ConsumeKey(KeyCode.LeftArrow) || context.ConsumeKey(KeyCode.UpArrow)) next = Mathf.Max(0, i - 1);
                    else if (context.ConsumeKey(KeyCode.RightArrow) || context.ConsumeKey(KeyCode.DownArrow)) next = Mathf.Min(options.Length - 1, i + 1);
                    else if (context.ConsumeKey(KeyCode.Home)) next = 0;
                    else if (context.ConsumeKey(KeyCode.End)) next = options.Length - 1;
                    if (next != i) { selected = next; context.Focus(ids[next]); }
                }
                visuals[i].Step(context, interaction, enabled);
                focused |= enabled && interaction.Focused;
                hover = Mathf.Max(hover, visuals[i].Hover);
                using (context.Opacity(visuals[i].Opacity))
                {
                    Color hoverInk = palette.Ink;
                    hoverInk.a = 0.05f * visuals[i].Hover;
                    context.Fill(item, hoverInk);
                }
            }
            if (!initialized) { selectedPosition.Snap(selected); initialized = true; }
            float position = selectedPosition.Step(selected, context.Time, UiMotion.Standard, context.ReducedMotion);
            Rect indicator = new Rect(rect.x + inset + position * width, rect.y + inset, width, height);
            using (context.Opacity(visuals[selected].Opacity))
                context.Fill(indicator, palette.Selected);
            for (int i = 0; i < options.Length; i++)
            {
                Rect item = new Rect(rect.x + inset + width * i, rect.y + inset, width, height);
                float proximity = Mathf.Clamp01(1f - Mathf.Abs(position - i));
                Color ink = Color.Lerp(palette.Ink, palette.OnSelected, proximity);
                using (context.Opacity(visuals[i].Opacity))
                {
                    context.Label(item, options[i], ink, TextAnchor.MiddleCenter);
                }
            }
            line.Draw(context, rect, focused, hover, scale: scale);
            if (selected != previous) GUI.changed = true;
            return selected;
        }
    }
}
