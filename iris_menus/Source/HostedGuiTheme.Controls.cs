using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using IrisMenus.UI;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace IrisMenus
{
    internal sealed partial class HostedGuiTheme
    {
#if RIMWORLD_1_7
        private enum HostedControlKind { Slider, Checkbox, Button, Toggle, Input, Tab, Category }

        private readonly struct HostedControlKey : IEquatable<HostedControlKey>
        {
            private readonly HostedControlKind kind;
            private readonly Rect rect;
            private readonly ulong groups;
            private readonly int name, occurrence;

            internal HostedControlKey(HostedControlKind kind, Rect rect, ulong groups, int name = 0, int occurrence = 0)
            { this.kind = kind; this.rect = rect; this.groups = groups; this.name = name; this.occurrence = occurrence; }
            internal HostedControlKey At(int index) => new HostedControlKey(kind, rect, groups, name, index);
            public bool Equals(HostedControlKey other) => kind == other.kind && rect == other.rect && groups == other.groups && name == other.name && occurrence == other.occurrence;
            public override bool Equals(object obj) => obj is HostedControlKey other && Equals(other);
            public override int GetHashCode() => unchecked((((((int)kind * 397 ^ rect.GetHashCode()) * 397 ^ groups.GetHashCode()) * 397) ^ name) * 397 ^ occurrence);
        }

        private sealed class HostedControl
        {
            internal UiSlider Slider;
            internal UiCheckbox Checkbox;
            internal UiSwitch Switch;
            internal RectOffset TogglePadding;
            internal UiButton Button;
            internal UiControlLine Line;
            internal UiControlVisual Visual;
            internal UiTween Selection;
            internal bool SelectionInitialized;
            internal int SeenFrame;
        }

        private readonly Dictionary<HostedControlKey, HostedControl> hostedControls = new Dictionary<HostedControlKey, HostedControl>();
        private readonly Dictionary<HostedControlKey, int> controlOccurrences = new Dictionary<HostedControlKey, int>();
        private readonly Dictionary<GUIStyle, GUIStyle> hostedPaintStyles = new Dictionary<GUIStyle, GUIStyle>();
        private readonly List<HostedControlKey> expiredControls = new List<HostedControlKey>();
        private UiContext controlsContext;
        private string controlsPage;
        private bool hostedButtonEnabled = true;
        private int controlsPrunedFrame = -1;
        private static readonly Func<int?> ConsumeNativeControlName = AccessTools.MethodDelegate<Func<int?>>(AccessTools.Method(typeof(GUI), "ConsumeNextControlName"));
        private static readonly Action PlayNativeSliderChange = AccessTools.MethodDelegate<Action>(AccessTools.Method(typeof(Widgets), "CheckPlayDragSliderSound"));

        private static readonly Texture2D NativeButtonHoverAtlas = (Texture2D)AccessTools.Field(typeof(Widgets), "ButtonBGAtlasMouseover").GetValue(null);
        private HostedControl Control(HostedControlKind kind, Rect rect, int name = 0)
        {
            // Group rectangles are in their parent's content coordinates. Unlike screen origins,
            // they do not move when a scroll view scrolls; nested groups remain distinct.
            ulong groups = 14695981039346656037UL;
            var backend = UnityStubs.Gui.GuiBackend.Current;
            if (backend != null)
            {
                for (int i = 0; i < backend.GroupCount; i++)
                {
                    backend.GetGroup(i, out Rect group, out _);
                    groups = unchecked((groups ^ (uint)group.GetHashCode()) * 1099511628211UL);
                }
            }
            var key = new HostedControlKey(kind, rect, groups, name);
            controlOccurrences.TryGetValue(key, out int occurrence);
            controlOccurrences[key] = occurrence + 1;
            key = key.At(occurrence);
            if (!hostedControls.TryGetValue(key, out HostedControl retained))
            {
                retained = new HostedControl();
                hostedControls.Add(key, retained);
            }
            retained.SeenFrame = context.Frame;
            return retained;
        }

        private void Release(HostedControl retained)
        {
            if (retained.Slider == null || controlsContext == null) return;
            controlsContext.Release(retained.Slider.Id);
            if (controlsContext.IsFocused(retained.Slider.Id)) controlsContext.Focus(default);
        }

        private void BeginControls(string identity)
        {
            if (!ReferenceEquals(controlsContext, context) || !string.Equals(controlsPage, identity, StringComparison.Ordinal))
            {
                DisposeControls();
                controlsContext = context;
                controlsPage = identity;
            }
            controlOccurrences.Clear();
            hostedButtonEnabled = true;
            if (controlsPrunedFrame == context.Frame) return;
            controlsPrunedFrame = context.Frame;
            foreach (var pair in hostedControls)
                if (context.Frame - pair.Value.SeenFrame > 120) expiredControls.Add(pair.Key);
            foreach (HostedControlKey key in expiredControls)
            { Release(hostedControls[key]); hostedControls.Remove(key); }
            expiredControls.Clear();
        }

        private void DisposeControls()
        {
            foreach (HostedControl retained in hostedControls.Values) Release(retained);
            hostedControls.Clear();
            controlOccurrences.Clear();
            hostedPaintStyles.Clear();
            expiredControls.Clear();
            controlsContext = null;
            controlsPage = null;
            controlsPrunedFrame = -1;
        }

        private static HostedGuiTheme ActiveControls => current != null && !current.bypass ? current : null;

        internal static bool TryGetInputCaretColor(GUIStyle style, out Color color)
        {
            HostedGuiTheme owner = ActiveControls;
            if (owner == null || style?.name != "IrisMenusHostedInput")
            {
                color = default;
                return false;
            }
            color = owner.palette.Ink;
            color.a *= GUI.color.a;
            return true;
        }

        private UiInteraction NativeInteraction(Rect rect, bool enabled, bool focused = false)
        {
            bool hover = enabled && GUI.enabled && context.Hit(rect);
            return new UiInteraction(hover, hover && Input.GetMouseButton(0), focused, false);
        }

        private GUIStyle PaintStyle(GUIStyle source)
        {
            if (!hostedPaintStyles.TryGetValue(source, out GUIStyle target))
            {
                target = new GUIStyle(source);
                hostedPaintStyles.Add(source, target);
            }
            // Keep consumer typography/layout mutations without allocating another style.
            target.name = source.name;
            target.font = source.font;
            target.fontSize = source.fontSize;
            target.fontStyle = source.fontStyle;
            target.alignment = source.alignment;
            target.wordWrap = source.wordWrap;
            target.richText = source.richText;
            target.clipping = source.clipping;
            target.imagePosition = source.imagePosition;
            target.padding = source.padding;
            target.contentOffset = source.contentOffset;
            CopyPaintState(source.normal, target.normal);
            CopyPaintState(source.hover, target.hover);
            CopyPaintState(source.active, target.active);
            CopyPaintState(source.focused, target.focused);
            CopyPaintState(source.onNormal, target.onNormal);
            CopyPaintState(source.onHover, target.onHover);
            CopyPaintState(source.onActive, target.onActive);
            CopyPaintState(source.onFocused, target.onFocused);
            return target;
        }

        private static void CopyPaintState(GUIStyleState source, GUIStyleState target)
        { target.background = null; target.textColor = source.textColor; }

        private static bool CommonBackground(Texture2D texture, GUIStyle standard)
        {
            return texture == null || texture == standard.normal.background || texture == standard.hover.background ||
                texture == standard.active.background || texture == standard.focused.background ||
                texture == standard.onNormal.background || texture == standard.onHover.background ||
                texture == standard.onActive.background || texture == standard.onFocused.background;
        }

        private static bool CommonStyle(GUIStyle style, GUIStyle standard)
        {
            return style != null && CommonBackground(style.normal.background, standard) &&
                CommonBackground(style.hover.background, standard) && CommonBackground(style.active.background, standard) &&
                CommonBackground(style.focused.background, standard) && CommonBackground(style.onNormal.background, standard) &&
                CommonBackground(style.onHover.background, standard) && CommonBackground(style.onActive.background, standard) &&
                CommonBackground(style.onFocused.background, standard);
        }

        private void DrawHostedButton(Rect rect, bool enabled)
        {
            HostedControl retained = Control(HostedControlKind.Button, rect);
            if (retained.Button == null) retained.Button = new UiButton(context);
            retained.Button.DrawVisual(rect, null, NativeInteraction(rect, enabled), enabled && GUI.enabled,
                variant: UiButtonVariant.Light, drawContent: false);
        }

        private float DrawHostedSlider(Rect identity, Rect input, Rect visual, float value, float left, float right, int name = 0)
        {
            HostedControl retained = Control(HostedControlKind.Slider, identity, name);
            if (retained.Slider == null) retained.Slider = new UiSlider(context);
            bool finite = !float.IsNaN(left) && !float.IsNaN(right) && !float.IsInfinity(left) && !float.IsInfinity(right);
            bool varying = finite && left != right;
            float normalized = varying ? Mathf.Clamp01((float)(((double)value - left) / ((double)right - left))) : 0f;
            if (float.IsNaN(normalized)) normalized = 0f;
            float before = normalized;
            bool enabled = varying && GUI.enabled;
            var backend = UnityStubs.Gui.GuiBackend.Current;
            enabled &= backend == null || !backend.InlineControlsObscured;
            bool dragging = retained.Slider.IsDragging;
            bool priorBypass = bypass;
            float result;
            try
            {
                bypass = true;
                result = retained.Slider.DrawHosted(visual, input, normalized, enabled);
            }
            finally { bypass = priorBypass; }
            if (!dragging && retained.Slider.IsDragging) SoundDefOf.DragSlider.PlayOneShotOnCamera();
            // Do not silently clamp consumer values on non-input passes. A constant range
            // still paints a track, but cannot manufacture a different caller-owned value.
            if (!varying || result == before) return value;
            return (float)(left + ((double)right - left) * result);
        }

        private static void InstallControls(Harmony harmony)
        {
            PatchControl(harmony, typeof(Widgets), nameof(Widgets.HorizontalSlider), typeof(HostedWidgetSlider), "Prefix",
                new[] { typeof(Rect), typeof(float), typeof(float), typeof(float), typeof(bool), typeof(string), typeof(string), typeof(string), typeof(float) });
            PatchControl(harmony, typeof(GUI), nameof(GUI.HorizontalSlider), typeof(HostedGuiSlider), "Prefix",
                new[] { typeof(Rect), typeof(float), typeof(float), typeof(float) });
            PatchControl(harmony, typeof(Widgets), nameof(Widgets.CheckboxDraw), typeof(HostedCheckbox), "Prefix");
            PatchControl(harmony, typeof(Widgets), nameof(Widgets.CheckboxLabeled), typeof(HostedLabeledSwitch), "Prefix");
            PatchControl(harmony, typeof(Widgets), nameof(Widgets.DrawButtonGraphic), typeof(HostedButtonGraphic), "Prefix");
            PatchControl(harmony, typeof(Widgets), nameof(Widgets.DrawAtlas), typeof(HostedButtonAtlas), "Prefix",
                new[] { typeof(Rect), typeof(Texture2D), typeof(bool) });
            PatchControl(harmony, typeof(GUI), "DrawTextFieldChrome", typeof(HostedInputChrome), "Prefix");
            harmony.Patch(AccessTools.Method(typeof(GUI), "DoTextField", new[] {
                typeof(Rect), typeof(int), typeof(GUIContent), typeof(bool), typeof(int), typeof(GUIStyle), typeof(string), typeof(char) }),
                prefix: new HarmonyMethod(typeof(HostedInputChange), "Prefix"),
                postfix: new HarmonyMethod(typeof(HostedInputChange), "Postfix"));
            PatchControl(harmony, typeof(GUIStyle), nameof(GUIStyle.Draw), typeof(HostedStyleDraw), "Prefix",
                new[] { typeof(Rect), typeof(GUIContent), typeof(bool), typeof(bool), typeof(bool), typeof(bool) });
            var worker = AccessTools.Method(typeof(Widgets), "ButtonTextWorker");
            harmony.Patch(worker, prefix: new HarmonyMethod(typeof(HostedButtonWorker), "Prefix"),
                finalizer: new HarmonyMethod(typeof(HostedButtonWorker), "Finalizer"));
            InstallNavigation(harmony);
        }

        private static void PatchControl(Harmony harmony, Type type, string method, Type patch, string prefix, Type[] arguments = null) =>
            harmony.Patch(AccessTools.Method(type, method, arguments), prefix: new HarmonyMethod(patch, prefix));

        private static class HostedWidgetSlider
        {
            private static bool Prefix(Rect rect, float value, float min, float max, bool middleAlignment, string label,
                string leftAlignedLabel, string rightAlignedLabel, float roundTo, ref float __result)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null) return true;
                Rect rail = rect;
                if (middleAlignment || !string.IsNullOrEmpty(label)) rail.y += Mathf.Round((rail.height - 10f) / 2f);
                if (!string.IsNullOrEmpty(label)) rail.y += 5f;
                // Vanilla's rail is centered six units below its adjusted input rectangle.
                Rect visual = new Rect(rail.x, rail.y - 4f, rail.width, 20f);
                float result = owner.DrawHostedSlider(rect, rail, visual, value, min, max);
                if (!string.IsNullOrEmpty(label) || !string.IsNullOrEmpty(leftAlignedLabel) || !string.IsNullOrEmpty(rightAlignedLabel))
                {
                    TextAnchor anchor = Text.Anchor;
                    GameFont font = Text.Font;
                    try
                    {
                        Text.Font = GameFont.Small;
                        rail.y -= (string.IsNullOrEmpty(label) ? 18f : Text.CalcSize(label).y) - 3f;
                        if (!string.IsNullOrEmpty(leftAlignedLabel)) { Text.Anchor = TextAnchor.UpperLeft; Widgets.Label(rail, leftAlignedLabel); }
                        if (!string.IsNullOrEmpty(rightAlignedLabel)) { Text.Anchor = TextAnchor.UpperRight; Widgets.Label(rail, rightAlignedLabel); }
                        if (!string.IsNullOrEmpty(label)) { Text.Anchor = TextAnchor.UpperCenter; Widgets.Label(rail, label); }
                    }
                    finally { Text.Anchor = anchor; Text.Font = font; }
                }
                if (roundTo > 0f) result = Mathf.RoundToInt(result / roundTo) * roundTo;
                if (result != value) PlayNativeSliderChange();
                __result = result;
                return false;
            }
        }

        private static class HostedGuiSlider
        {
            private static bool Prefix(Rect position, float value, float leftValue, float rightValue, ref float __result)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null) return true;
                int name = ConsumeNativeControlName() ?? 0;
                __result = owner.DrawHostedSlider(position, position, position, value,
                    Mathf.Min(leftValue, rightValue), Mathf.Max(leftValue, rightValue), name);
                return false;
            }
        }

        private static class HostedLabeledSwitch
        {
            private static bool Prefix(Rect rect, string label, ref bool checkOn, bool disabled,
                Texture2D texChecked, Texture2D texUnchecked, bool placeCheckboxNearText, bool paintable)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null || (texChecked != null && texChecked != Widgets.CheckboxOnTex) ||
                    (texUnchecked != null && texUnchecked != Widgets.CheckboxOffTex)) return true;
                TextAnchor anchor = Text.Anchor;
                try
                {
                    Text.Anchor = TextAnchor.MiddleLeft;
                    float unit = Mathf.Max(0f, Mathf.Min(1f, rect.height / 28f));
                    float width = 52f * unit;
                    if (placeCheckboxNearText) rect.width = Mathf.Min(rect.width, Text.CalcSize(label).x + width + 12f * unit);
                    Rect text = rect;
                    text.xMax -= width + 12f * unit;
                    Widgets.Label(text, label);
                    bool before = checkOn;
                    if (!disabled) Widgets.ToggleInvisibleDraggable(rect, ref checkOn, doMouseoverSound: true, paintable: paintable);
                    HostedControl retained = owner.Control(HostedControlKind.Toggle, rect);
                    if (retained.Switch == null) retained.Switch = new UiSwitch(owner.context);
                    Rect track = new Rect(rect.xMax - width, rect.y, width, rect.height);
                    retained.Switch.DrawVisual(track, checkOn, owner.NativeInteraction(rect, !disabled),
                        enabled: !disabled && GUI.enabled, scale: unit);
                    if (checkOn != before) GUI.changed = true;
                }
                finally { Text.Anchor = anchor; }
                return false;
            }
        }

        private static class HostedCheckbox
        {
            private static bool Prefix(float x, float y, bool active, bool disabled, float size, Texture2D texChecked, Texture2D texUnchecked)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null || (texChecked != null && texChecked != Widgets.CheckboxOnTex) ||
                    (texUnchecked != null && texUnchecked != Widgets.CheckboxOffTex)) return true;
                Rect rect = new Rect(x, y, size, size);
                HostedControl retained = owner.Control(HostedControlKind.Checkbox, rect);
                if (retained.Checkbox == null) retained.Checkbox = new UiCheckbox(owner.context);
                retained.Checkbox.DrawVisual(rect, active, owner.NativeInteraction(rect, !disabled),
                    enabled: !disabled && GUI.enabled, scale: size / 24f);
                return false;
            }
        }

        private static class HostedButtonGraphic
        {
            private static bool Prefix(Rect rect)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null) return true;
                owner.DrawHostedButton(rect, owner.hostedButtonEnabled);
                return false;
            }
        }

        private static class HostedButtonAtlas
        {
            private static bool Prefix(Rect rect, Texture2D atlas)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null || (atlas != Widgets.ButtonBGAtlas && atlas != Widgets.ButtonBGAtlasClick && atlas != NativeButtonHoverAtlas)) return true;
                owner.DrawHostedButton(rect, owner.hostedButtonEnabled);
                return false;
            }
        }

        private readonly struct HostedButtonState
        {
            internal readonly HostedGuiTheme Owner;
            internal readonly bool Enabled;
            internal HostedButtonState(HostedGuiTheme owner) { Owner = owner; Enabled = owner.hostedButtonEnabled; }
        }

        private static class HostedButtonWorker
        {
            private static void Prefix(bool active, out HostedButtonState __state)
            {
                HostedGuiTheme owner = ActiveControls;
                __state = owner == null ? default : new HostedButtonState(owner);
                if (owner != null) owner.hostedButtonEnabled = active;
            }
            private static void Finalizer(HostedButtonState __state)
            { if (__state.Owner != null) __state.Owner.hostedButtonEnabled = __state.Enabled; }
        }

        private static class HostedInputChange
        {
            private static void Prefix(GUIContent content, out string __state) =>
                __state = ActiveControls != null ? content?.text ?? string.Empty : null;

            private static void Postfix(GUIContent content, string __state)
            {
                if (__state != null && !string.Equals(__state, content?.text ?? string.Empty, StringComparison.Ordinal))
                    GUI.changed = true;
            }
        }

        private static class HostedInputChrome
        {
            private static void Prefix(Rect rect, bool focused, bool multiline, ref GUIStyle style)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null || style?.name == "IrisMenusTextField") return;
                GUIStyle source = style ?? GUI.skin.textField;
                if (!CommonStyle(source, GUI.skin.textField)) return;
                // Read-only area detection already happened in TextFieldImpl with the original
                // style; this replacement is solely the native paint seam, never its editor.
                HostedControl retained = owner.Control(HostedControlKind.Input, rect);
                UiInteraction interaction = owner.NativeInteraction(rect, GUI.enabled, focused);
                retained.Visual.Step(owner.context, interaction, GUI.enabled);
                using (owner.context.Opacity(retained.Visual.Opacity))
                {
                    Color fill = source.name.IndexOf("Search", StringComparison.OrdinalIgnoreCase) >= 0
                        ? owner.palette.SearchInput : owner.palette.Input;
                    owner.context.Fill(rect, fill);
                    retained.Line.Draw(owner.context, rect, focused, retained.Visual.Hover);
                }
                style = owner.PaintStyle(source);
                style.name = "IrisMenusHostedInput";
            }
        }

        private static class HostedStyleDraw
        {
            private static bool Prefix(GUIStyle __instance, Rect position, GUIContent content, bool isHover,
                bool isActive, bool on, bool hasKeyboardFocus)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null || __instance.name == "IrisMenusHostedPaint") return true;
                bool toggle = __instance.name == GUI.skin.toggle.name && CommonStyle(__instance, GUI.skin.toggle);
                bool button = __instance.name == GUI.skin.button.name && CommonStyle(__instance, GUI.skin.button);
                if (!toggle && !button) return true;
                GUIStyle paint = owner.PaintStyle(__instance);
                if (toggle)
                {
                    HostedControl retained = owner.Control(HostedControlKind.Toggle, position);
                    if (retained.Switch == null) retained.Switch = new UiSwitch(owner.context);
                    float unit = Mathf.Max(0f, Mathf.Min(1f, Mathf.Min(position.width / 52f, position.height / 28f)));
                    Rect track = new Rect(position.x, position.y, 52f * unit, position.height);
                    retained.Switch.DrawVisual(track, on, new UiInteraction(isHover, isActive, hasKeyboardFocus, false),
                        enabled: GUI.enabled, scale: unit);
                    if (retained.TogglePadding == null) retained.TogglePadding = new RectOffset();
                    RectOffset padding = retained.TogglePadding;
                    padding.left = Mathf.Max(__instance.padding.left, Mathf.CeilToInt(64f * unit));
                    padding.right = __instance.padding.right;
                    padding.top = __instance.padding.top;
                    padding.bottom = __instance.padding.bottom;
                    paint.padding = padding;
                }
                else owner.DrawHostedButton(position, GUI.enabled);
                paint.name = "IrisMenusHostedPaint";
                paint.Draw(position, content, isHover, isActive, on, hasKeyboardFocus);
                return false;
            }
        }
#else
        private static void InstallControls(Harmony harmony) { }
        private void BeginControls(string identity) { }
        private void DisposeControls() { }
#endif
    }
}
