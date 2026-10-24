using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiTextField : IDisposable
    {
        private readonly UiContext context;
        private readonly string controlName;
        private readonly int maxLength;
        private GUIStyle style;
        private UiControlVisual visual;
        private UiTween error;
        private int editorHotControl;

        public UiTextField(UiContext context, int maxLength = -1)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (maxLength < -1) throw new ArgumentOutOfRangeException(nameof(maxLength));
            this.maxLength = maxLength;
            Id = context.CreateId();
            controlName = "IrisMenus.UI.TextField." + Guid.NewGuid().ToString("N");
        }

        public UiId Id { get; }
        public bool Submitted { get; private set; }

        // The returned string remains caller-owned. Native text editing supplies selection,
        // caret movement, clipboard shortcuts, horizontal scrolling and IME composition.
        public string Draw(Rect rect, string value, string placeholder = null, bool enabled = true, bool invalid = false)
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.textField)
                {
                    name = "IrisMenusTextField",
                    alignment = TextAnchor.MiddleLeft,
                    wordWrap = false,
                    richText = false,
                    padding = new RectOffset(8, 8, 3, 3)
                };
                style.normal.background = null;
                style.hover.background = null;
                style.active.background = null;
                style.focused.background = null;
                style.onNormal.background = null;
                style.onHover.background = null;
                style.onActive.background = null;
                style.onFocused.background = null;
            }
            value = value ?? string.Empty;
            Event current = context.Event;
            EventType original = current != null ? current.type : EventType.Ignore;
            bool wasCaptured = context.IsCaptured(Id);
            UiInteraction interaction = context.Interact(Id, rect, enabled, false);
            bool active = enabled && context.IsInputAllowed && context.IsFocused(Id);
            Submitted = active && (context.ConsumeKey(KeyCode.Return) || context.ConsumeKey(KeyCode.KeypadEnter));
            visual.Step(context, interaction, enabled);
            float invalidAmount = error.Step(invalid ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
            UiPalette palette = context.Palette;
            using (context.Opacity(visual.Opacity))
            {
                context.Fill(rect, visual.Background(palette, palette.Sunken));
                Color border = Color.Lerp(palette.Line, palette.StrongLine, visual.Hover);
                border = Color.Lerp(border, palette.Signal, visual.Focus);
                context.Border(rect, Color.Lerp(border, palette.Error, invalidAmount), UiMotion.Lerp(1f, 2f, Mathf.Max(visual.Focus, invalidAmount)));
                SetTextColor(palette.Ink);
                string focusedName = GUI.GetNameOfFocusedControl();
                if (active)
                {
                    if (focusedName != controlName) GUI.FocusControl(controlName);
                }
                else if (focusedName == controlName)
                {
                    GUI.FocusControl(null);
                    editorHotControl = 0;
                }
                bool mouseDown = original == EventType.MouseDown && context.IsCaptured(Id);
                bool mouseDrag = original == EventType.MouseDrag && context.IsCaptured(Id);
                bool mouseUp = original == EventType.MouseUp && wasCaptured;
                bool replay = active && (mouseDown || mouseUp);
                bool nativeMouse = active && (mouseDown || mouseDrag || mouseUp);
                bool inputEvent = original == EventType.MouseDown || original == EventType.MouseDrag || original == EventType.MouseUp || original == EventType.KeyDown || original == EventType.KeyUp || original == EventType.ScrollWheel;
                EventType consumed = current != null ? current.type : EventType.Ignore;
                int outerHot = GUIUtility.hotControl;
                Color outerColor = GUI.color;
                Color outerContent = GUI.contentColor;
                Color outerBackground = GUI.backgroundColor;
                bool outerEnabled = GUI.enabled;
                try
                {
                    GUI.enabled = outerEnabled && active;
                    GUI.color = new Color(1f, 1f, 1f, context.Alpha);
                    GUI.contentColor = Color.white;
                    GUI.backgroundColor = Color.clear;
                    // Some supported backends do not honor GUI.enabled for text editing.
                    // Hide disallowed input locally rather than letting it reach that backend.
                    if (current != null)
                    {
                        if (replay) current.type = original;
                        else if (inputEvent && (!active ||
                            (original == EventType.KeyDown && current.keyCode == KeyCode.Tab) ||
                            (!nativeMouse && original != EventType.KeyDown && original != EventType.KeyUp))) current.type = EventType.Ignore;
                    }
                    GUIUtility.hotControl = active ? editorHotControl : 0;
                    GUI.SetNextControlName(controlName);
                    value = GUI.TextField(rect, value, maxLength, style);
                    if (active) editorHotControl = GUIUtility.hotControl;
                }
                finally
                {
                    GUIUtility.hotControl = outerHot;
                    GUI.color = outerColor;
                    GUI.contentColor = outerContent;
                    GUI.backgroundColor = outerBackground;
                    GUI.enabled = outerEnabled;
                    // A consumed event stays consumed after the private native editor replay.
                    if (current != null && (replay || current.type == EventType.Ignore)) current.type = consumed;
                }
                if (mouseDrag && active) context.Consume();
                if (!active && value.Length == 0 && !string.IsNullOrEmpty(placeholder))
                    context.Label(new Rect(rect.x + 8f, rect.y, Mathf.Max(0f, rect.width - 16f), rect.height), placeholder, palette.Muted);
            }
            return value;
        }

        // Call when a retained field is removed from a surface, to release only its native focus.
        public void Dispose()
        {
            context.Release(Id);
            if (context.IsFocused(Id)) context.Focus(default(UiId));
            if (GUI.GetNameOfFocusedControl() == controlName) GUI.FocusControl(null);
            editorHotControl = 0;
        }

        private void SetTextColor(Color color)
        {
            style.normal.textColor = color;
            style.hover.textColor = color;
            style.active.textColor = color;
            style.focused.textColor = color;
            style.onNormal.textColor = color;
            style.onHover.textColor = color;
            style.onActive.textColor = color;
            style.onFocused.textColor = color;
        }
    }
}
