#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using IrisMenus.UI;
using UnityEngine;
using UnityStubs.Gui;
using Verse;

namespace IrisMenus
{
    internal sealed partial class HostedGuiTheme
    {
        private enum HostedNavigationKind { None, Tabs, Categories, Sections, Header }
        private struct HostedNavigationPaint
        {
            internal HostedGuiTheme Owner;
            internal HostedNavigationKind Kind;
            internal Rect Item;
            internal bool HasItem;
            internal float Selection;
        }
        [ThreadStatic] private static HostedNavigationPaint navigationPaint;

        private static void InstallNavigation(Harmony harmony)
        {
            PatchControl(harmony, typeof(TabRecord), nameof(TabRecord.Draw), typeof(HostedTabRecord), "Prefix", new[] { typeof(Rect) });
            var scrollPaint = new HarmonyMethod(typeof(HostedGuiTheme), nameof(PatchScrollbarPaint));
            harmony.Patch(AccessTools.Method(typeof(GUI), "HandleScrollbars",
                new[] { typeof(Rect), typeof(Vector2), typeof(Rect), typeof(float), typeof(float) }), transpiler: scrollPaint);
            harmony.Patch(AccessTools.Method(typeof(GUI), "Scrollbar",
                new[] { typeof(Rect), typeof(float), typeof(float), typeof(float), typeof(float), typeof(bool) }), transpiler: scrollPaint);
            PatchControl(harmony, typeof(Widgets), nameof(Widgets.DrawBoxSolid), typeof(HostedNavigationFill), "Prefix",
                new[] { typeof(Rect), typeof(Color) });
            harmony.Patch(AccessTools.Method(typeof(Widgets), nameof(Widgets.Label), new[] { typeof(Rect), typeof(string) }),
                prefix: new HarmonyMethod(typeof(HostedNavigationLabel), "Prefix"),
                finalizer: new HarmonyMethod(typeof(HostedNavigationLabel), "Finalizer"));
            harmony.Patch(AccessTools.Method(typeof(Widgets), nameof(Widgets.DrawBox), new[] { typeof(Rect), typeof(int), typeof(Texture2D) }),
                prefix: new HarmonyMethod(typeof(HostedNavigationBox), "Prefix"),
                finalizer: new HarmonyMethod(typeof(HostedNavigationBox), "Finalizer"));
            PatchNavigationChrome(harmony, nameof(Widgets.DrawLineHorizontal), new[] { typeof(float), typeof(float), typeof(float) });
            PatchNavigationChrome(harmony, nameof(Widgets.DrawLineVertical), new[] { typeof(float), typeof(float), typeof(float) });
            PatchControl(harmony, typeof(Widgets), nameof(Widgets.DrawHighlight), typeof(HostedNavigationHighlight), "Prefix", new[] { typeof(Rect) });

            // These deployed mods implement tabs themselves, not through TabDrawer. Limit
            // their palette adaptation to the proven painting methods, never arbitrary textures.
            PatchOptionalNavigation(harmony, "RimTalk.Settings", "DrawTabButtons");
            PatchOptionalNavigation(harmony, "RimTalk.Settings", "DrawApiModeCard");
            PatchOptionalNavigation(harmony, "RimTalk.Settings", "DrawSinglePresetBox");
            PatchOptionalNavigation(harmony, "RimTalk.Settings", "DrawAdvancedApiSettings");
            PatchOptionalNavigation(harmony, "RimTalk.Settings", "DoSettingsWindowContents");
            PatchOptionalNavigation(harmony, "RimTalk.Settings", "DrawQuickSetup");
            PatchOptionalNavigation(harmony, "RimChat.Config.RimChatSettings", "DrawTabs");
            PatchOptionalNavigation(harmony, "RimChat.Config.RimChatSettings", "DrawSectionHeader");
            PatchOptionalNavigation(harmony, "RimChat.Config.RimChatSettings", "DrawNavigationPanelWithButtons");
            PatchOptionalNavigation(harmony, "RimChat.Config.RimChatSettings", "DrawRPGNavigationPanel");
            PatchOptionalNavigation(harmony, "RimChat.Config.RimChatSettings", "DrawRimTalkChannelSelector");
        }

        private static void PatchNavigationChrome(Harmony harmony, string name, Type[] arguments) =>
            harmony.Patch(AccessTools.Method(typeof(Widgets), name, arguments),
                prefix: new HarmonyMethod(typeof(HostedNavigationChrome), "Prefix"),
                finalizer: new HarmonyMethod(typeof(HostedNavigationChrome), "Finalizer"));

        private static void PatchOptionalNavigation(Harmony harmony, string typeName, string methodName)
        {
            Type type = AccessTools.TypeByName(typeName);
            MethodInfo method = type == null ? null : AccessTools.DeclaredMethod(type, methodName);
            if (method == null) return;
            harmony.Patch(method, prefix: new HarmonyMethod(typeof(HostedCustomNavigation), "Prefix"),
                finalizer: new HarmonyMethod(typeof(HostedCustomNavigation), "Finalizer"));
        }

        private float DrawHostedNavigation(Rect rect, bool selected, bool vertical, HostedControl retained)
        {
            bool enabled = GUI.enabled;
            retained.Visual.Step(context, NativeInteraction(rect, enabled), enabled);
            if (!retained.SelectionInitialized)
            { retained.Selection.Snap(selected ? 1f : 0f); retained.SelectionInitialized = true; }
            float selection = retained.Selection.Step(selected ? 1f : 0f, context.Time, UiMotion.Enter, context.ReducedMotion);
            using (context.Opacity(retained.Visual.Opacity * GUI.color.a))
            {
                Color hover = palette.Hover;
                hover.a *= Mathf.Max(retained.Visual.Hover, retained.Visual.Press) * (1f - selection);
                context.Fill(rect, hover);
                Color fill = palette.Selected; fill.a *= selection;
                context.Fill(rect, fill);
                context.Fill(new Rect(rect.x, rect.yMax - 1f, rect.width, 1f), palette.StrongLine);
                if (vertical && selection > 0f)
                {
                    Color signal = palette.Signal; signal.a *= selection;
                    context.Fill(new Rect(rect.x, rect.center.y - Mathf.Min(24f, rect.height) * 0.5f, 3f, Mathf.Min(24f, rect.height)), signal);
                }
                if (!vertical && selection > 0f && rect.width >= 76f)
                {
                    using (context.Opacity(selection))
                    {
                        Rect arrow = new Rect(rect.xMax - 27f, rect.center.y - 10f, 20f, 20f);
                        context.Fill(arrow, palette.Raised);
                        Vector2 center = arrow.center;
                        context.Triangle(new Vector2(center.x - 2f, center.y - 3f), new Vector2(center.x + 3f, center.y),
                            new Vector2(center.x - 2f, center.y + 3f), palette.Ink);
                    }
                }
                retained.Visual.DrawFocus(context, rect);
            }
            return selection;
        }

        private static class HostedTabRecord
        {
            private static bool Prefix(TabRecord __instance, Rect rect)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null) return true;
                HostedControl retained = owner.Control(HostedControlKind.Tab, rect);
                float selection = owner.DrawHostedNavigation(rect, __instance.Selected, false, retained);
                Color color = GUI.color; bool wrap = Text.WordWrap; bool bypass = owner.bypass;
                try
                {
                    owner.bypass = true;
                    Color ink = Color.Lerp(owner.palette.Ink, owner.palette.OnSelected, selection);
                    ink.a *= color.a * owner.alpha * retained.Visual.Opacity;
                    GUI.color = ink; Text.WordWrap = false;
                    if (!__instance.TutorTag.NullOrEmpty()) UIHighlighter.HighlightOpportunity(rect, __instance.TutorTag);
                    Rect text = rect;
                    if (rect.width >= 76f) { text.x += 5f; text.width -= 30f * selection + 10f; }
                    Widgets.Label(text, __instance.label);
                }
                finally { GUI.color = color; Text.WordWrap = wrap; owner.bypass = bypass; }
                // TabDrawer retains the complete original hit loop, tooltip, sound and callback.
                return false;
            }
        }

        private static IEnumerable<CodeInstruction> PatchScrollbarPaint(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            MethodInfo fill = AccessTools.Method(typeof(IGuiBackend), nameof(IGuiBackend.FillRect));
            MethodInfo rounded = AccessTools.Method(typeof(IGuiBackend), nameof(IGuiBackend.FillRectRounded));
            MethodInfo replacement = AccessTools.Method(typeof(HostedGuiTheme), nameof(DrawHostedScrollbarPart));
            MethodInfo roundedReplacement = AccessTools.Method(typeof(HostedGuiTheme), nameof(DrawHostedRoundedScrollbarPart));
            int part = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                bool isFill = instruction.Calls(fill), isRounded = instruction.Calls(rounded);
                if (!isFill && !isRounded) { yield return instruction; continue; }
                int index = part++;
                var kind = new CodeInstruction(OpCodes.Ldc_I4, index % 2);
                kind.labels.AddRange(instruction.labels); kind.blocks.AddRange(instruction.blocks);
                instruction.labels.Clear(); instruction.blocks.Clear();
                yield return kind;
                yield return __originalMethod.Name == "Scrollbar" ? new CodeInstruction(OpCodes.Ldarg_S, (byte)5) :
                    new CodeInstruction(index >= 2 ? OpCodes.Ldc_I4_1 : OpCodes.Ldc_I4_0);
                instruction.opcode = OpCodes.Call; instruction.operand = isRounded ? roundedReplacement : replacement;
                yield return instruction;
            }
            int expected = __originalMethod.Name == "HandleScrollbars" ? 4 : 2;
            if (part != expected) throw new InvalidOperationException("IrisMenus could not locate native scrollbar painting in " + __originalMethod.Name + ".");
        }

        private static void DrawHostedScrollbarPart(IGuiBackend backend, Rect rect, Color color, int part, bool horizontal)
        {
            HostedGuiTheme owner = ActiveControls;
            if (owner == null) { backend.FillRect(rect, color); return; }
            owner.DrawScrollbarPart(rect, part, horizontal);
        }

        private static void DrawHostedRoundedScrollbarPart(IGuiBackend backend, Rect rect, Color color, float radius, int part, bool horizontal)
        {
            HostedGuiTheme owner = ActiveControls;
            if (owner == null) { backend.FillRectRounded(rect, color, radius); return; }
            owner.DrawScrollbarPart(rect, part, horizontal);
        }

        private void DrawScrollbarPart(Rect rect, int part, bool horizontal)
        {
            using (context.Opacity(GUI.color.a * (GUI.enabled ? 1f : 0.42f)))
            {
                if (part == 0)
                {
                    Color track = palette.StrongLine; track.a *= 0.35f;
                    context.Fill(rect, track);
                    return;
                }
                // Preserve the native thumb's travel, size and hit region. Only its ink
                // becomes UiScrollView's straight three-unit theme-interpolated rail.
                Rect thumb = horizontal ? new Rect(rect.x, rect.yMax - 3f, rect.width, 3f) :
                    new Rect(rect.xMax - 3f, rect.y, 3f, rect.height);
                context.Fill(thumb, Color.Lerp(Color.black, Color.white, context.ThemeAmount));
            }
        }

        private static class HostedCustomNavigation
        {
            private static void Prefix(MethodBase __originalMethod, out HostedNavigationPaint __state)
            {
                __state = navigationPaint;
                HostedGuiTheme owner = ActiveControls;
                if (owner == null) return;
                string name = __originalMethod.Name;
                HostedNavigationKind kind = name == "DrawTabButtons" || name == "DrawTabs" ? HostedNavigationKind.Tabs :
                    name == "DrawSectionHeader" ? HostedNavigationKind.Header :
                    name == "DoSettingsWindowContents" || name == "DrawQuickSetup" ? HostedNavigationKind.Sections : HostedNavigationKind.Categories;
                navigationPaint = new HostedNavigationPaint { Owner = owner, Kind = kind };
            }
            private static void Finalizer(HostedNavigationPaint __state) { navigationPaint = __state; }
        }

        private static class HostedNavigationFill
        {
            private static bool Prefix(Rect rect, ref Color color)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null || navigationPaint.Owner != owner) return true;
                HostedNavigationKind kind = navigationPaint.Kind;
                bool selected = kind == HostedNavigationKind.Tabs ?
                    SameRgb(color, new Color(11f / 85f, 11f / 85f, 11f / 85f)) || SameRgb(color, new Color(0.2f, 0.4f, 0.6f)) :
                    SameRgb(color, new Color(0.22f, 0.33f, 0.45f)) || SameRgb(color, new Color(0.25f, 0.35f, 0.55f));
                bool inactive = kind == HostedNavigationKind.Tabs ?
                    SameRgb(color, new Color(0.08627451f, 0.08627451f, 0.08627451f)) || SameRgb(color, new Color(0.15f, 0.15f, 0.15f)) :
                    SameRgb(color, new Color(0.1f, 0.11f, 0.13f)) || SameRgb(color, new Color(0.18f, 0.18f, 0.2f)) ||
                    SameRgb(color, new Color(0.2f, 0.22f, 0.28f));
                if (kind != HostedNavigationKind.Sections && rect.width > 3f && (selected || inactive))
                {
                    HostedControl retained = owner.Control(kind == HostedNavigationKind.Tabs ? HostedControlKind.Tab : HostedControlKind.Category, rect);
                    navigationPaint.Item = rect; navigationPaint.HasItem = true;
                    navigationPaint.Selection = owner.DrawHostedNavigation(rect, selected, kind != HostedNavigationKind.Tabs, retained);
                    return false;
                }
                Color mapped;
                if (rect.width <= 3f && SameRgb(color, new Color(0.4f, 0.7f, 1f))) return false;
                if (SameRgb(color, new Color(11f / 85f, 11f / 85f, 11f / 85f)) ||
                    SameRgb(color, new Color(0.12f, 0.12f, 0.14f)) || SameRgb(color, new Color(0.1f, 0.1f, 0.12f))) mapped = owner.palette.Sunken;
                else if (rect.height <= 2f && SameRgb(color, new Color(0.3f, 0.3f, 0.3f))) mapped = owner.palette.StrongLine;
                else return true;
                mapped.a *= color.a * owner.alpha; color = mapped;
                return true;
            }
        }

        private readonly struct HostedNavigationLabelState
        {
            internal readonly HostedGuiTheme Owner;
            internal readonly Color Color;
            internal readonly bool Bypass;
            internal HostedNavigationLabelState(HostedGuiTheme owner) { Owner = owner; Color = GUI.color; Bypass = owner.bypass; }
        }

        private static class HostedNavigationLabel
        {
            private static void Prefix(Rect rect, out HostedNavigationLabelState __state)
            {
                HostedGuiTheme owner = ActiveControls;
                __state = default;
                if (owner == null || navigationPaint.Owner != owner) return;
                bool item = navigationPaint.HasItem && rect.xMin >= navigationPaint.Item.xMin && rect.xMax <= navigationPaint.Item.xMax + 1f &&
                    rect.yMin >= navigationPaint.Item.yMin && rect.yMax <= navigationPaint.Item.yMax + 1f;
                if (!item && navigationPaint.Kind != HostedNavigationKind.Header) return;
                __state = new HostedNavigationLabelState(owner);
                Color ink = item ? Color.Lerp(owner.palette.Ink, owner.palette.OnSelected, navigationPaint.Selection) : owner.palette.Ink;
                ink.a *= GUI.color.a * owner.alpha * (GUI.enabled ? 1f : 0.42f);
                GUI.color = ink; owner.bypass = true;
            }
            private static void Finalizer(HostedNavigationLabelState __state)
            {
                if (__state.Owner == null) return;
                GUI.color = __state.Color; __state.Owner.bypass = __state.Bypass;
            }
        }

        private static class HostedNavigationChrome
        {
            internal static void Prefix(out HostedNavigationLabelState __state)
            {
                HostedGuiTheme owner = ActiveControls; __state = default;
                if (owner == null || navigationPaint.Owner != owner) return;
                if (navigationPaint.Kind == HostedNavigationKind.Sections &&
                    !SameRgb(GUI.color, new Color(14f / 51f, 14f / 51f, 14f / 51f))) return;
                __state = new HostedNavigationLabelState(owner);
                Color line = owner.palette.StrongLine; line.a *= GUI.color.a * owner.alpha; GUI.color = line;
            }
            private static void Finalizer(HostedNavigationLabelState __state)
            { if (__state.Owner != null) GUI.color = __state.Color; }

            internal static void BeginBox(Texture2D texture, out HostedNavigationLabelState state)
            {
                state = default;
                if (texture == null || texture == BaseContent.WhiteTex) Prefix(out state);
            }
        }

        private static class HostedNavigationBox
        {
            private static void Prefix(Texture2D lineTexture, out HostedNavigationLabelState __state) =>
                HostedNavigationChrome.BeginBox(lineTexture, out __state);
            private static void Finalizer(HostedNavigationLabelState __state)
            { if (__state.Owner != null) GUI.color = __state.Color; }
        }

        private static class HostedNavigationHighlight
        {
            private static bool Prefix(Rect rect)
            {
                HostedGuiTheme owner = ActiveControls;
                if (owner == null || navigationPaint.Owner != owner) return true;
                if (!navigationPaint.HasItem || rect != navigationPaint.Item)
                {
                    Color hover = owner.palette.Hover; hover.a *= GUI.color.a;
                    owner.context.Fill(rect, hover);
                }
                return false;
            }
        }
    }
}
#endif
