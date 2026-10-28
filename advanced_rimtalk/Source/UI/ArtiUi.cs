using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Verse;

namespace AdvancedRimTalk.UI
{
    internal static class ArtiUi
    {
#if ART_IRISMENUS
        [ThreadStatic] internal static bool IsNative;
#else
        internal static bool IsNative => false;
#endif
        internal static bool IsLight => IsNative && ArtiNativeUi.IsLight;
        internal static Color Ink => IsNative ? ArtiNativeUi.Ink : Color.white;
        internal static Color Muted => IsNative ? ArtiNativeUi.Muted : Color.gray;
        internal static Color Surface => IsNative ? ArtiNativeUi.Surface : Widgets.WindowBGFillColor;
        internal static Color Panel => IsNative ? ArtiNativeUi.Panel : Widgets.MenuSectionBGFillColor;
        internal static Color Border => IsNative ? ArtiNativeUi.Border : Color.gray;
        internal static Color Accent => IsNative ? ArtiNativeUi.Accent : new Color(.45f, .8f, .9f);
        internal static Color Danger => IsNative ? ArtiNativeUi.Danger : new Color(1f, .45f, .45f);
        internal static Color Warning => IsNative ? ArtiNativeUi.Warning : new Color(1f, .8f, .3f);
        internal static Color Success => IsNative ? ArtiNativeUi.Success : new Color(.5f, .85f, .6f);

        internal static void Label(Rect rect, string label)
        {
            if (IsNative) ArtiNativeUi.Label(rect, label);
            else Widgets.Label(rect, label);
        }
        internal static void Label(Rect rect, string label, Color color)
        {
            if (IsNative) { ArtiNativeUi.Label(rect, label, color); return; }
            Color saved = GUI.color;
            GUI.color = color;
            try { Widgets.Label(rect, label); }
            finally { GUI.color = saved; }
        }
        internal static bool ButtonText(Rect rect, string label, bool drawBackground = true, bool doMouseoverSound = true,
            bool active = true, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsNative ? ArtiNativeUi.ButtonText(rect, label, drawBackground, doMouseoverSound, active, file, line)
                : Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active);
        internal static bool Navigation(Rect rect, string label, bool selected,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (IsNative) return ArtiNativeUi.Navigation(rect, label, selected, file, line);
            if (selected) Widgets.DrawHighlightSelected(rect);
            return Widgets.ButtonText(rect, label, false);
        }
        internal static string TextField(Rect rect, string value,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsNative ? ArtiNativeUi.TextField(rect, value, file, line) : Widgets.TextField(rect, value);
        internal static string TextArea(Rect rect, string value,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsNative ? ArtiNativeUi.TextArea(rect, value, file, line) : Widgets.TextArea(rect, value);
        internal static void CheckboxLabeled(Rect rect, string label, ref bool value,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (IsNative) ArtiNativeUi.CheckboxLabeled(rect, label, ref value, file, line);
            else Widgets.CheckboxLabeled(rect, label, ref value);
        }
        internal static bool RadioButtonLabeled(Rect rect, string label, bool active,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsNative ? ArtiNativeUi.RadioButtonLabeled(rect, label, active, file, line) : Widgets.RadioButtonLabeled(rect, label, active);
        internal static void DrawMenuSection(Rect rect)
        { if (IsNative) ArtiNativeUi.DrawMenuSection(rect); else Widgets.DrawMenuSection(rect); }
        internal static void DrawHighlight(Rect rect)
        { if (IsNative) ArtiNativeUi.DrawHighlight(rect); else Widgets.DrawHighlight(rect); }
        internal static void DrawHighlightSelected(Rect rect)
        { if (IsNative) ArtiNativeUi.DrawHighlightSelected(rect); else Widgets.DrawHighlightSelected(rect); }
        internal static void BeginScrollView(Rect rect, ref Vector2 position, Rect viewRect,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (IsNative) ArtiNativeUi.BeginScrollView(rect, ref position, viewRect, file, line);
            else Widgets.BeginScrollView(rect, ref position, viewRect);
        }
        internal static void EndScrollView()
        { if (IsNative) ArtiNativeUi.EndScrollView(); else Widgets.EndScrollView(); }
        internal static void ListingLabel(Listing_Standard listing, string label)
        { if (IsNative) ArtiNativeUi.ListingLabel(listing, label); else listing.Label(label); }
        internal static bool ListingRadio(Listing_Standard listing, string label, bool active, float gap,
            string tooltip, string highlightTag, [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsNative ? ArtiNativeUi.ListingRadio(listing, label, active, gap, tooltip, highlightTag, file, line)
                : listing.RadioButton(label, active, gap, tooltip, null);
        internal static void ListingCheckbox(Listing_Standard listing, string label, ref bool value, string tooltip,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (IsNative) ArtiNativeUi.ListingCheckbox(listing, label, ref value, tooltip, file, line);
            else listing.CheckboxLabeled(label, ref value, tooltip);
        }
        internal static bool ListingButton(Listing_Standard listing, string label,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0) =>
            IsNative ? ArtiNativeUi.ListingButton(listing, label, file, line) : listing.ButtonText(label);
        internal static void TextFieldNumeric(Rect rect, ref int value, ref string buffer, int minimum, int maximum,
            [CallerFilePath] string file = "", [CallerLineNumber] int line = 0)
        {
            if (IsNative) ArtiNativeUi.TextFieldNumeric(rect, ref value, ref buffer, minimum, maximum, file, line);
            else Widgets.TextFieldNumeric(rect, ref value, ref buffer, minimum, maximum);
        }
    }
}
