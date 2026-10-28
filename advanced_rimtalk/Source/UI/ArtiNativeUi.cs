#if ART_IRISMENUS
extern alias iris;
using iris::IrisMenus.UI;
#endif
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace AdvancedRimTalk.UI
{
    internal static class ArtiNativeUi
    {
#if ART_IRISMENUS
        [ThreadStatic] internal static NativeState Current;
        internal static bool IsNative => Current != null;
        internal static bool IsLight => Current != null && Current.Context.ThemeAmount < 0.5f;
        internal static Color Ink => Current?.Context.Palette.Ink ?? Color.white;
        internal static Color Muted => Current?.Context.Palette.Muted ?? Color.gray;
        internal static Color Surface => Current?.Context.Palette.Surface ?? Widgets.WindowBGFillColor;
        internal static Color Panel => Current?.Context.Palette.Sunken ?? Widgets.MenuSectionBGFillColor;
        internal static Color Border => Current?.Context.Palette.StrongLine ?? Color.gray;
        internal static Color Danger => Current?.Context.Palette.Error ?? new Color(1f, .45f, .45f);
#else
        internal static bool IsNative => false;
        internal static bool IsLight => false;
        internal static Color Ink => Color.white;
        internal static Color Muted => Color.gray;
        internal static Color Surface => Widgets.WindowBGFillColor;
        internal static Color Panel => Widgets.MenuSectionBGFillColor;
        internal static Color Border => Color.gray;
        internal static Color Danger => new Color(1f, .45f, .45f);
#endif
        internal static Color Accent => IsLight ? new Color(.18f, .37f, .52f) : new Color(.45f, .8f, .9f);
        internal static Color Warning => IsLight ? new Color(.52f, .35f, .05f) : new Color(1f, .8f, .3f);
        internal static Color Success => IsLight ? new Color(.19f, .4f, .24f) : new Color(.5f, .85f, .6f);

        internal static void Label(Rect rect, string label) => Label(rect, label, GUI.color == Color.white ? Ink : GUI.color);
        internal static void Label(Rect rect, string label, Color color)
        {
#if ART_IRISMENUS
            if (Current != null)
            {
                float size = Current.Context.FontSize;
                Current.Context.FontSize = Text.Font == GameFont.Tiny ? 12f : Text.Font == GameFont.Medium ? 22f : 16f;
                try { Current.Context.Label(rect, label, color, Text.Anchor, Text.WordWrap); }
                finally { Current.Context.FontSize = size; }
                return;
            }
#endif
            Color saved = GUI.color;
            GUI.color = color;
            try { Widgets.Label(rect, label); }
            finally { GUI.color = saved; }
        }

        internal static bool ButtonText(Rect rect, string label, bool drawBackground = true,
            bool doMouseoverSound = true, bool active = true,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if ART_IRISMENUS
            if (Current != null)
            {
                Control control = Current.Get(file, line);
                UiGlyphKind icon = label switch
                {
                    "+" => UiGlyphKind.Plus,
                    "×" => UiGlyphKind.Close,
                    "▲" => UiGlyphKind.ArrowUp,
                    "▼" => UiGlyphKind.ArrowDown,
                    _ => UiGlyphKind.None
                };
                if (icon != UiGlyphKind.None)
                    return control.Button.Draw(rect, string.Empty, GUI.enabled && active, variant: UiButtonVariant.Ghost, icon: icon);
                if (rect.width < 70f)
                {
                    UiInteraction input = Current.Context.Interact(control.Button.Id, rect, GUI.enabled && active);
                    float hover = control.Selection.Step(input.Hovered || input.Pressed ? 1f : 0f,
                        Current.Context.Time, UiMotion.Fast, Current.Context.ReducedMotion);
                    Current.Context.Fill(rect, Color.Lerp(Current.Context.Palette.Button, Current.Context.Palette.Hover, hover));
                    Current.Context.Label(rect, label, Ink, TextAnchor.MiddleCenter);
                    return input.Clicked;
                }
                return control.Button.Draw(rect, label, GUI.enabled && active,
                    variant: drawBackground ? UiButtonVariant.Light : UiButtonVariant.Ghost);
            }
#endif
            return Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active);
        }

        internal static bool Navigation(Rect rect, string label, bool selected,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if ART_IRISMENUS
            if (Current != null)
            {
                Control control = Current.Get(file, line);
                UiContext context = Current.Context;
                UiInteraction interaction = context.Interact(control.Button.Id, rect, GUI.enabled);
                float amount = control.Selection.Step(selected || interaction.Hovered || interaction.Pressed ? 1f : 0f,
                    context.Time, UiMotion.Fast, context.ReducedMotion);
                Color fill = context.Palette.Selected;
                fill.a *= amount;
                context.Fill(rect, fill);
                Rect text = new Rect(rect.x + 10f, rect.y, Mathf.Max(0f, rect.width - 20f), rect.height);
                using (context.Clip(text))
                    context.Label(new Rect(0f, 0f, text.width, text.height), label,
                        Color.Lerp(context.Palette.Ink, context.Palette.OnSelected, amount));
                if (selected) context.Fill(new Rect(rect.x, rect.y + 6f, 3f, Mathf.Max(0f, rect.height - 12f)), context.Palette.Signal);
                if (interaction.Focused && context.FocusVisible) context.Border(rect, context.Palette.Ink);
                return interaction.Clicked;
            }
#endif
            if (selected) Widgets.DrawHighlightSelected(rect);
            return Widgets.ButtonText(rect, label, false);
        }

        internal static string TextField(Rect rect, string text,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if ART_IRISMENUS
            if (Current != null)
            {
                Control control = Current.Get(file, line);
                control.Field ??= new UiTextField(Current.Context);
                return control.Field.Draw(rect, text, enabled: GUI.enabled);
            }
#endif
            return Widgets.TextField(rect, text);
        }

        internal static string TextArea(Rect rect, string text,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if ART_IRISMENUS
            if (Current != null)
            {
                Control control = Current.Get(file, line);
                control.Area ??= new GUIStyle(Text.CurTextAreaStyle) { name = "AdvancedRimTalk.NativeArea", richText = false };
                SetTextColor(control.Area, Ink);
                Current.Context.Fill(rect, Current.Context.Palette.Input);
                return GUI.TextArea(rect, text ?? string.Empty, control.Area);
            }
#endif
            return Widgets.TextArea(rect, text);
        }

        internal static void CheckboxLabeled(Rect rect, string label, ref bool value,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if ART_IRISMENUS
            if (Current != null)
            {
                Control control = Current.Get(file, line);
                control.Toggle ??= new UiSwitch(Current.Context);
                value = control.Toggle.Draw(rect, value, label, GUI.enabled);
                return;
            }
#endif
            Widgets.CheckboxLabeled(rect, label, ref value);
        }

        internal static bool RadioButtonLabeled(Rect rect, string label, bool active,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if ART_IRISMENUS
            if (Current != null) return Navigation(rect, label, active, file, line);
#endif
            return Widgets.RadioButtonLabeled(rect, label, active);
        }

        internal static void DrawMenuSection(Rect rect)
        {
#if ART_IRISMENUS
            if (Current != null)
            {
                Current.Context.Fill(rect, Panel);
                Current.Context.Border(rect, Current.Context.Palette.Line);
                return;
            }
#endif
            Widgets.DrawMenuSection(rect);
        }
        internal static void DrawHighlight(Rect rect)
        {
#if ART_IRISMENUS
            if (Current != null) { Current.Context.Fill(rect, Current.Context.Palette.Hover); return; }
#endif
            Widgets.DrawHighlight(rect);
        }
        internal static void DrawHighlightSelected(Rect rect) => DrawHighlight(rect);

        internal static void BeginScrollView(Rect rect, ref Vector2 position, Rect viewRect,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
#if ART_IRISMENUS
            if (Current != null) { Current.BeginScroll(rect, ref position, viewRect, file, line); return; }
#endif
            Widgets.BeginScrollView(rect, ref position, viewRect);
        }
        internal static void EndScrollView()
        {
#if ART_IRISMENUS
            if (Current != null) { Current.EndScroll(); return; }
#endif
            Widgets.EndScrollView();
        }

        private static void SetTextColor(GUIStyle style, Color color)
        {
            style.normal.textColor = style.hover.textColor = style.active.textColor = style.focused.textColor = color;
            style.normal.background = style.hover.background = style.active.background = style.focused.background = null;
        }

        internal static void ListingLabel(Listing_Standard listing, string label)
        {
            if (!IsNative) { listing.Label(label); return; }
            Label(listing.GetRect(Mathf.Max(28f, Text.CalcHeight(label, listing.ColumnWidth))), label);
            listing.Gap(8f);
        }

        internal static bool ListingRadio(Listing_Standard listing, string label, bool active, float gap,
            string tooltip, string highlightTag, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsNative) return listing.RadioButton(label, active, gap, tooltip, null);
            Rect row = listing.GetRect(Mathf.Max(32f, Text.CalcHeight(label, listing.ColumnWidth - 20f)));
            TooltipHandler.TipRegion(row, tooltip);
            bool clicked = RadioButtonLabeled(row, label, active, file, line);
            listing.Gap(4f);
            return clicked;
        }

        internal static void ListingCheckbox(Listing_Standard listing, string label, ref bool value, string tooltip,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsNative) { listing.CheckboxLabeled(label, ref value, tooltip); return; }
            Rect row = listing.GetRect(Mathf.Max(36f, Text.CalcHeight(label, listing.ColumnWidth - 72f)));
            TooltipHandler.TipRegion(row, tooltip);
            CheckboxLabeled(row, label, ref value, file, line);
            listing.Gap(4f);
        }

        internal static bool ListingButton(Listing_Standard listing, string label,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsNative) return listing.ButtonText(label);
            return ButtonText(listing.GetRect(32f), label, file: file, line: line);
        }

        internal static void TextFieldNumeric(Rect rect, ref int value, ref string buffer, int minimum, int maximum,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (!IsNative) { Widgets.TextFieldNumeric(rect, ref value, ref buffer, minimum, maximum); return; }
            buffer = TextField(rect, buffer, file, line);
            if (int.TryParse(buffer, out int parsed)) value = Mathf.Clamp(parsed, minimum, maximum);
        }

#if ART_IRISMENUS
        internal sealed class Control : IDisposable
        {
            internal readonly UiButton Button;
            internal UiTextField Field;
            internal UiSwitch Toggle;
            internal GUIStyle Area;
            internal UiTween Selection;
            internal int Seen;
            internal Control(UiContext context) { Button = new UiButton(context); }
            public void Dispose() { Field?.Dispose(); }
        }

        internal sealed class NativeState : IDisposable
        {
            internal readonly UiContext Context;
            private readonly Dictionary<(string, int), int> occurrences = new();
            private readonly Dictionary<(string, int, int), Control> controls = new();
            private readonly Dictionary<(string, int, int), ScrollState> scrolls = new();
            private readonly Stack<(UiContext.UiScope, UiContext.UiScope)> scrollScopes = new();
            private int pass;
            internal NativeState(UiContext context) { Context = context; }
            internal void Begin() { occurrences.Clear(); pass++; }
            private (string, int, int) Key(string file, int line)
            {
                var site = (file, line);
                occurrences.TryGetValue(site, out int occurrence);
                occurrences[site] = occurrence + 1;
                return (file, line, occurrence);
            }
            internal Control Get(string file, int line)
            {
                var key = Key(file, line);
                if (!controls.TryGetValue(key, out Control control)) controls.Add(key, control = new Control(Context));
                control.Seen = pass;
                return control;
            }
            internal void End()
            {
                foreach (Control control in controls.Values)
                    if (control.Seen != pass)
                    {
                        control.Field?.Dispose();
                        Context.Release(control.Button.Id);
                        if (control.Toggle != null) Context.Release(control.Toggle.Id);
                    }
            }
            internal void BeginScroll(Rect rect, ref Vector2 position, Rect view, string file, int line)
            {
                var key = Key(file, line);
                if (!scrolls.TryGetValue(key, out ScrollState scroll)) scrolls.Add(key, scroll = new ScrollState(Context));
                scroll.Bar(rect, view, ref position, false);
                scroll.Bar(rect, view, ref position, true);
                UiContext.UiScope viewport = Context.Clip(rect);
                Rect local = new Rect(0f, 0f, rect.width, rect.height);
                position = GUI.BeginScrollView(local, position, view, GUIStyle.none, GUIStyle.none);
                UiContext.UiScope content = Context.Clip(view);
                scrollScopes.Push((viewport, content));
            }
            internal void EndScroll()
            {
                var scopes = scrollScopes.Pop();
                scopes.Item2.Dispose();
                GUI.EndScrollView();
                scopes.Item1.Dispose();
            }
            public void Dispose()
            {
                foreach (Control control in controls.Values)
                {
                    control.Dispose();
                    Context.Release(control.Button.Id);
                    if (control.Toggle != null) Context.Release(control.Toggle.Id);
                }
                foreach (ScrollState scroll in scrolls.Values) scroll.Dispose();
                controls.Clear(); scrolls.Clear();
            }
        }

        private sealed class ScrollState : IDisposable
        {
            private readonly UiContext context;
            private readonly UiId vertical, horizontal;
            private float dragMouse, dragPosition, dragRange, dragTravel;
            internal ScrollState(UiContext context)
            { this.context = context; vertical = context.CreateId(); horizontal = context.CreateId(); }
            internal void Bar(Rect rect, Rect view, ref Vector2 position, bool x)
            {
                float extent = x ? rect.width : rect.height;
                float length = x ? view.width : view.height;
                float maximum = Mathf.Max(0f, length - extent);
                float value = Mathf.Clamp(x ? position.x : position.y, 0f, maximum);
                UiId id = x ? horizontal : vertical;
                Rect track = x ? new Rect(rect.x, rect.yMax - 12f, rect.width - 12f, 12f)
                    : new Rect(rect.xMax - 12f, rect.y, 12f, rect.height - (view.width > rect.width ? 12f : 0f));
                float trackLength = x ? track.width : track.height;
                float thumb = Mathf.Min(trackLength, Mathf.Max(24f, trackLength * extent / Mathf.Max(1f, length)));
                float travel = Mathf.Max(1f, trackLength - thumb);
                float start = x ? track.x : track.y;
                float top = start + value / Mathf.Max(1f, maximum) * travel;
                Event input = context.Event;
                EventType type = input?.type ?? EventType.Ignore;
                bool captured = context.IsCaptured(id);
                UiInteraction interaction = context.Interact(id, maximum > 0f ? track : default, maximum > 0f && GUI.enabled, false);
                float mouse = input == null ? 0f : x ? input.mousePosition.x : input.mousePosition.y;
                if (!captured && context.IsCaptured(id) && type == EventType.MouseDown)
                {
                    if (mouse < top || mouse > top + thumb) value = Mathf.Clamp01((mouse - start - thumb * .5f) / travel) * maximum;
                    dragMouse = mouse; dragPosition = value; dragRange = maximum; dragTravel = travel;
                }
                if (captured && context.IsInputAllowed && (type == EventType.MouseDrag || type == EventType.MouseUp))
                {
                    value = Mathf.Clamp(dragPosition + (mouse - dragMouse) / dragTravel * dragRange, 0f, maximum);
                    if (type == EventType.MouseDrag) context.Consume();
                }
                if (maximum > 0f)
                {
                    Rect bar = x ? new Rect(start + value / maximum * travel, track.yMax - 3f, thumb, 3f)
                        : new Rect(track.xMax - 3f, start + value / maximum * travel, 3f, thumb);
                    context.Fill(bar, context.Palette.Ink);
                    if (interaction.Focused && context.FocusVisible) context.Border(track, context.Palette.Ink);
                }
                if (x) position.x = value; else position.y = value;
            }
            public void Dispose() { context.Release(vertical); context.Release(horizontal); }
        }
#endif
    }
}
