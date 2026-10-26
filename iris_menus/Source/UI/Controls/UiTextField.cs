using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiTextField : IDisposable
    {
        private readonly UiContext context;
        private readonly string controlName;
        private readonly UiId clearId;
        private readonly int maxLength;
        private GUIStyle style;
        private UiControlVisual visual;
        private UiControlVisual clearVisual;
        private UiTween error;
        private int editorHotControl;

        public UiTextField(UiContext context, int maxLength = -1)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            if (maxLength < -1) throw new ArgumentOutOfRangeException(nameof(maxLength));
            this.maxLength = maxLength;
            Id = context.CreateId();
            clearId = context.CreateId();
            controlName = "IrisMenus.UI.TextField." + Guid.NewGuid().ToString("N");
        }

        public UiId Id { get; }
        public bool Submitted { get; private set; }

        // The returned string remains caller-owned. Native text editing supplies selection,
        // caret movement, clipboard shortcuts, horizontal scrolling and IME composition.
        public string Draw(Rect rect, string value, string placeholder = null, bool enabled = true, bool invalid = false,
            UiGlyphKind icon = UiGlyphKind.None, string shortcut = null, bool showClear = false, float scale = 1f)
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
            style.fontSize = Mathf.RoundToInt(context.FontSize);
            style.fontStyle = context.FontStyle;
            style.padding.left = style.padding.right = Mathf.RoundToInt(8f * scale);
            style.padding.top = style.padding.bottom = Mathf.RoundToInt(3f * scale);
            value = value ?? string.Empty;
            string originalValue = value;
            Event current = context.Event;
            EventType original = current != null ? current.type : EventType.Ignore;
            bool wasCaptured = context.IsCaptured(Id);
            float unit = Mathf.Max(0f, scale);
            bool clearVisible = showClear && value.Length > 0;
            Rect endRect = new Rect(rect.xMax - 32f * unit, rect.y, 28f * unit, rect.height);
            if (enabled && context.IsInputAllowed && context.IsFocused(Id) && current != null
                && original == EventType.MouseDown && !rect.Contains(current.mousePosition))
            {
                context.Focus(default);
                context.Release(Id);
                editorHotControl = 0;
                if (GUI.GetNameOfFocusedControl() == controlName) GUI.FocusControl(null);
            }
            UiInteraction clearInteraction = context.Interact(clearId, clearVisible ? endRect : default(Rect), enabled && clearVisible);
            clearVisual.Step(context, clearInteraction, enabled);
            if (clearInteraction.Clicked) { value = string.Empty; context.Focus(Id); }
            float endWidth = clearVisible ? 36f * unit : !string.IsNullOrEmpty(shortcut) ? 62f * unit : 0f;
            float startWidth = icon == UiGlyphKind.None ? 0f : 28f * unit;
            Rect editorRect = new Rect(rect.x + startWidth, rect.y, Mathf.Max(0f, rect.width - startWidth - endWidth), rect.height);
            UiInteraction interaction = context.Interact(Id, rect, enabled, false);
            bool active = enabled && context.IsInputAllowed && context.IsFocused(Id);
            Submitted = active && (context.ConsumeKey(KeyCode.Return) || context.ConsumeKey(KeyCode.KeypadEnter));
            visual.Step(context, interaction, enabled);
            float invalidAmount = error.Step(invalid ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
            UiPalette palette = context.Palette;
            Color inputFill = icon == UiGlyphKind.Search ? palette.SearchInput : palette.Input;
            using (context.Opacity(visual.Opacity))
            {
                context.Fill(rect, inputFill);
                if (icon != UiGlyphKind.None)
                    UiGlyph.Draw(context, new Rect(rect.x + 10f * unit, rect.center.y - 8f * unit, 16f * unit, 16f * unit), icon, palette.Muted);
                if (clearVisible)
                {
                    Color background = palette.Hover;
                    background.a *= clearVisual.Hover;
                    context.Fill(endRect, background);
                    UiGlyph.Draw(context, new Rect(endRect.center.x - 7f * unit, endRect.center.y - 7f * unit, 14f * unit, 14f * unit), UiGlyphKind.Close, palette.Muted);
                    clearVisual.DrawFocus(context, endRect);
                }
                else if (!string.IsNullOrEmpty(shortcut))
                {
                    Rect key = new Rect(rect.xMax - 54f * unit, rect.center.y - 10f * unit, 46f * unit, 20f * unit);
                    context.Border(key, palette.Line);
                    float fontSize = context.FontSize;
                    context.FontSize = 11f * unit;
                    context.Label(key, shortcut, palette.Muted, TextAnchor.MiddleCenter);
                    context.FontSize = fontSize;
                }
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
                    value = GUI.TextField(editorRect, value, maxLength, style);
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
                    context.Label(new Rect(editorRect.x + 8f * unit, editorRect.y, Mathf.Max(0f, editorRect.width - 16f * unit), editorRect.height), placeholder, palette.Muted);
            }
            if (!string.Equals(value, originalValue, StringComparison.Ordinal)) GUI.changed = true;
            return value;

        }
        // Call when a retained field is removed from a surface, to release only its native focus.
        public void Dispose()
        {
            context.Release(Id);
            context.Release(clearId);
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
