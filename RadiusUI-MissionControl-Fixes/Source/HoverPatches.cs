using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RadiusUI.MissionControl.Fixes
{
    internal static class HoverPatches
    {
        private delegate void DrawPane(int id, Rect anchor, string title, string body, Color accent, float scale);
        private delegate float MeasureText(string text, float width, bool bold, float pixels);

        private sealed class OverflowState
        {
            internal Rect anchor;
            internal Window owner;
            internal string title;
            internal string body;
            internal float offset;
        }

        private static Type paneStateType;
        private static Type messageStateType;
        private static Type handleStateType;
        private static Type quickPopoutType;
        private static DrawPane drawPane;
        private static MeasureText measureText;
        private static Func<float> suiteScale;
        private static Func<float> notificationScale;
        private static Func<MessageTypeDef, Color> messageColor;
        private static Action<Alert, Rect> drawAlertInfo;
        private static AccessTools.FieldRef<object, Message> stateMessage;
        private static AccessTools.FieldRef<object, Rect> stateCardRect;
        private static AccessTools.FieldRef<object, Color> stateAccent;
        private static AccessTools.FieldRef<object, float> stateWidth;
        private static AccessTools.FieldRef<object, bool> stateDocked;
        private static AccessTools.FieldRef<object, int> stateId;
        private static AccessTools.FieldRef<object, Rect> paneRect;
        private static AccessTools.FieldRef<object, string> paneTitle;
        private static AccessTools.FieldRef<object, string> paneBody;
        private static FieldInfo headHeight;
        private static FieldInfo titleField;
        private static MethodInfo inkLabel;
        private static MethodInfo inkWrapped;
        private static MethodInfo immediateWindow;
        private static readonly ConditionalWeakTable<object, OverflowState> overflow = new ConditionalWeakTable<object, OverflowState>();
        private static readonly ConditionalWeakTable<object, OverflowState>.CreateValueCallback createOverflow = CreateOverflow;

        internal static void Install(Harmony harmony)
        {
            Type hover = RequireType("RadiusUI.MissionControl.HoverPane");
            Type messages = RequireType("RadiusUI.MissionControl.MessageRenderer");
            Type alerts = RequireType("RadiusUI.MissionControl.AlertRenderer");
            Type ink = RequireType("RadiusUI.MissionControl.Ink");
            paneStateType = RequireType("RadiusUI.MissionControl.HoverPane+PaneState");
            messageStateType = RequireType("RadiusUI.MissionControl.MessageRenderer+MessageCardState");
            handleStateType = RequireType("RadiusUI.MissionControl.MessageRenderer+HandleState");
            quickPopoutType = RequireType("RadiusUI.MissionControl.QuickPopout");

            MethodInfo draw = RequireMethod(hover, "Draw", typeof(int), typeof(Rect), typeof(Texture2D), typeof(Color), typeof(string), typeof(string), typeof(float));
            MethodInfo body = RequireMethod(paneStateType, "Body");
            drawPane = AccessTools.MethodDelegate<DrawPane>(RequireMethod(hover, "Draw", typeof(int), typeof(Rect), typeof(string), typeof(string), typeof(Color), typeof(float)));
            measureText = AccessTools.MethodDelegate<MeasureText>(RequireMethod(ink, "Height", typeof(string), typeof(float), typeof(bool), typeof(float)));
            suiteScale = AccessTools.MethodDelegate<Func<float>>(RequireMethod(RequireType("RadiusUI.MissionControl.Column"), "get_SuiteScale"));
            notificationScale = AccessTools.MethodDelegate<Func<float>>(RequireMethod(messages, "get_BottomRightS"));
            messageColor = AccessTools.MethodDelegate<Func<MessageTypeDef, Color>>(RequireMethod(messages, "GetTypeColor", typeof(MessageTypeDef)));
            drawAlertInfo = AccessTools.MethodDelegate<Action<Alert, Rect>>(RequireMethod(alerts, "DrawAlertInfoPane", typeof(Alert), typeof(Rect)));
            stateMessage = FieldRef<Message>(messageStateType, "msg");
            stateCardRect = FieldRef<Rect>(messageStateType, "cardRect");
            stateAccent = FieldRef<Color>(messageStateType, "accent");
            stateWidth = FieldRef<float>(messageStateType, "explicitWidth");
            stateDocked = FieldRef<bool>(messageStateType, "docked");
            stateId = FieldRef<int>(messageStateType, "id");
            paneRect = FieldRef<Rect>(paneStateType, "paneRect");
            paneTitle = FieldRef<string>(paneStateType, "title");
            paneBody = FieldRef<string>(paneStateType, "body");
            headHeight = RequireField(paneStateType, "headH");
            titleField = RequireField(paneStateType, "title");
            inkLabel = RequireMethod(ink, "Label", typeof(Rect), typeof(string), typeof(Color), typeof(bool), typeof(float));
            inkWrapped = RequireMethod(ink, "LabelWrapped", typeof(Rect), typeof(string), typeof(Color), typeof(bool), typeof(float));
            immediateWindow = RequireMethod(typeof(WindowStack), "ImmediateWindow", typeof(int), typeof(Rect), typeof(WindowLayer), typeof(Action), typeof(bool), typeof(bool), typeof(float), typeof(Action), typeof(bool));
            MethodInfo mouseCovered = RequireMethod(hover, "MouseCoveredByForeignWindow");
            MethodInfo messageRow = RequireMethod(messages, "DrawMessageRow", typeof(Message), typeof(Rect), typeof(int));
            MethodInfo alertRow = RequireMethod(alerts, "DrawRow", typeof(Alert), typeof(Rect), typeof(int), typeof(bool), typeof(bool).MakeByRefType());
            MethodInfo notificationBody = RequireMethod(messageStateType, "Body");

            // Check both IL edits before applying any patch; Bootstrap rolls back
            // the entire owner if an upstream layout or signature is incompatible.
            DrawPaneTranspiler(PatchProcessor.GetOriginalInstructions(draw));
            PaneBodyTranspiler(PatchProcessor.GetOriginalInstructions(body));
            harmony.Patch(mouseCovered, prefix: PatchMethod(nameof(MouseCoveredPrefix)));
            harmony.Patch(draw, prefix: PatchMethod(nameof(DrawPanePrefix)), transpiler: PatchMethod(nameof(DrawPaneTranspiler)));
            harmony.Patch(body, prefix: PatchMethod(nameof(PaneBodyPrefix)), transpiler: PatchMethod(nameof(PaneBodyTranspiler)), finalizer: PatchMethod(nameof(PaneBodyFinalizer)));
            harmony.Patch(messageRow, prefix: PatchMethod(nameof(MessageRowPrefix)));
            harmony.Patch(alertRow, postfix: PatchMethod(nameof(AlertRowPostfix)));
            harmony.Patch(notificationBody, postfix: PatchMethod(nameof(NotificationBodyPostfix)));
        }

        internal static bool MouseCoveredPrefix(ref bool __result)
        {
            __result = MouseCovered(Find.WindowStack, UI.MousePosUIInvertedUseEventIfCan);
            return false;
        }

        internal static bool MouseCovered(WindowStack stack, Vector2 mouse, Window owner = null)
        {
            // ImmediateWindow IDs are already normalized correctly by upstream.
            // Its per-frame whitelist is not: windows/cached callbacks outlive it.
            // Identify live callbacks instead, without trusting arbitrary negative IDs.
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                Window window = stack[i];
                if (!window.windowRect.Contains(mouse))
                    continue;
                if (window is ImmediateWindow immediate)
                {
                    Type target = immediate.doWindowFunc?.Target?.GetType();
                    if (target == paneStateType)
                        continue; // Our tooltip must not hide a foreign window beneath it.
                    if (target == messageStateType || target == handleStateType)
                        return false;
                }
                // The popout owns its own rows, but must still occlude the HUD
                // behind it. Do not whitelist all Mission Control dialogs.
                return window != (owner ?? stack.currentlyDrawnWindow) || window.GetType() != quickPopoutType;
            }
            return false;
        }

        internal static void DrawPanePrefix(ref Rect cardRect)
        {
            Window owner = Find.WindowStack.currentlyDrawnWindow;
            if (owner != null && owner.GetType() == quickPopoutType)
                cardRect = UI.GUIToScreenRect(cardRect);
        }

        internal static void MessageRowPrefix(Message msg, Rect row)
        {
            if (Event.current.type == EventType.Repaint && !string.IsNullOrEmpty(msg.text) && Mouse.IsOver(row))
                drawPane(Gen.HashCombineInt(RuntimeHelpers.GetHashCode(msg), 1296388935), row, null, msg.text, messageColor(msg.def), suiteScale());
        }

        internal static void AlertRowPostfix(Alert alert, Rect row, bool hovered)
        {
            Window owner = Find.WindowStack.currentlyDrawnWindow;
            if (hovered && owner != null && owner.GetType() == quickPopoutType)
                drawAlertInfo(alert, row);
        }

        internal static void NotificationBodyPostfix(object __instance)
        {
            // Docked/bottom-right cards already provide the original hover pane.
            // Free notifications omit it even when multiline text is clipped.
            if (Event.current.type != EventType.Repaint || stateDocked(__instance) || stateWidth(__instance) > 0f)
                return;
            Message msg = stateMessage(__instance);
            Rect anchor = stateCardRect(__instance);
            if (msg != null && !string.IsNullOrEmpty(msg.text) && Mouse.IsOver(GenUI.AtZero(anchor)))
                drawPane(Gen.HashCombineInt(stateId(__instance), 1296388935), anchor, null, msg.text, stateAccent(__instance), notificationScale());
        }

        internal static float TitleHeight(string title, Texture2D icon, float scale, float paneWidth)
        {
            bool hasIcon = icon != null;
            if (string.IsNullOrEmpty(title))
                return hasIcon ? 22f * scale : 0f;
            float width = Mathf.Max(1f, paneWidth - 26f * scale - (hasIcon ? 30f * scale : 0f));
            return Mathf.Max(22f * scale, measureText(title, width, true, 15f));
        }

        internal static IEnumerable<CodeInstruction> DrawPaneTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int headStore = -1;
            int windowCall = -1;
            for (int i = 0; i < code.Count; i++)
            {
                if (code[i].opcode == OpCodes.Stfld && Equals(code[i].operand, headHeight))
                {
                    if (headStore >= 0 || i < 1 || !code[i - 1].IsLdloc())
                        throw Unsupported("header height store");
                    headStore = i;
                }
                if (code[i].Calls(immediateWindow))
                {
                    if (windowCall >= 0)
                        throw Unsupported("multiple pane windows");
                    windowCall = i;
                }
            }
            if (headStore < 0 || windowCall < 0)
                throw Unsupported("pane state/window binding");

            object heightLocal = code[headStore - 1].operand;
            // Both values are discovered from data flow, not decompiler local numbers.
            int heightStore = -1;
            int widthStore = -1;
            MethodInfo min = RequireMethod(typeof(Mathf), nameof(Mathf.Min), typeof(float), typeof(float));
            for (int i = 0; i < headStore; i++)
            {
                if (code[i].opcode == OpCodes.Stloc_S || code[i].opcode == OpCodes.Stloc)
                {
                    if (Equals(code[i].operand, heightLocal))
                    {
                        if (heightStore >= 0)
                            throw Unsupported("multiple header assignments");
                        heightStore = i;
                    }
                }
                if (code[i].Calls(min) && i + 1 < code.Count && IsStoreLocal(code[i + 1]))
                {
                    if (widthStore >= 0)
                        throw Unsupported("multiple pane widths");
                    widthStore = i + 1;
                }
            }
            if (heightStore < 0 || widthStore < 0 || widthStore >= heightStore)
                throw Unsupported("pane header/width locals");

            code[windowCall].opcode = OpCodes.Call;
            code[windowCall].operand = AccessTools.Method(typeof(HoverPatches), nameof(PaneWindow));
            var anchorLoad = new CodeInstruction(OpCodes.Ldarg_1);
            anchorLoad.labels.AddRange(code[windowCall].labels);
            code[windowCall].labels.Clear();
            code.Insert(windowCall, anchorLoad);
            CodeInstruction widthLoad = LoadLocal(code[widthStore]);
            code.InsertRange(heightStore + 1, new[]
            {
                new CodeInstruction(OpCodes.Ldarg_S, (byte)4),
                new CodeInstruction(OpCodes.Ldarg_2),
                new CodeInstruction(OpCodes.Ldarg_S, (byte)6),
                widthLoad,
                new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(HoverPatches), nameof(TitleHeight))),
                new CodeInstruction(code[heightStore].opcode, code[heightStore].operand)
            });
            return code;
        }

        internal static IEnumerable<CodeInstruction> PaneBodyTranspiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            int titleReads = 0;
            int matches = 0;
            foreach (CodeInstruction instruction in code)
            {
                if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, titleField))
                    titleReads++;
                if (instruction.Calls(inkLabel) && titleReads == 2)
                {
                    instruction.operand = inkWrapped;
                    matches++;
                    titleReads = 0;
                }
            }
            if (matches != 1)
                throw Unsupported("wrapped pane title");
            return code;
        }

        internal static void PaneWindow(WindowStack stack, int id, Rect rect, WindowLayer layer, Action draw,
            bool background, bool absorb, float shadow, Action outside, bool ignoreFader, Rect anchor)
        {
            if (rect.height > UI.screenHeight)
            {
                OverflowState state = overflow.GetValue(draw.Target, createOverflow);
                string title = paneTitle(draw.Target);
                string body = paneBody(draw.Target);
                if (!string.Equals(state.title, title, StringComparison.Ordinal) || !string.Equals(state.body, body, StringComparison.Ordinal))
                    state.offset = 0f;
                state.title = title;
                state.body = body;
                state.anchor = anchor;
                Window owner = stack.currentlyDrawnWindow;
                state.owner = owner != null && owner.GetType() == quickPopoutType ? owner : null;
                rect.y = 0f;
                rect.height = UI.screenHeight;
            }
            stack.ImmediateWindow(id, rect, layer, draw, background, absorb, shadow, outside, ignoreFader);
        }


        internal static void PaneBodyPrefix(object __instance, out bool __state)
        {
            __state = false;
            Rect content = paneRect(__instance);
            if (content.height <= UI.screenHeight || !overflow.TryGetValue(__instance, out OverflowState state))
                return;
            Event current = Event.current;
            // Keep the cursor on the source card and wheel through long content;
            // moving onto the tooltip must not change source-card click routing.
            if (current.type == EventType.ScrollWheel && state.anchor.Contains(UI.MousePosUIInvertedUseEventIfCan)
                && !MouseCovered(Find.WindowStack, UI.MousePosUIInvertedUseEventIfCan, state.owner))
            {
                state.offset += current.delta.y * 24f;
                current.Use();
            }
            state.offset = Mathf.Clamp(state.offset, 0f, content.height - UI.screenHeight);
            GUI.BeginGroup(new Rect(0f, 0f, content.width, UI.screenHeight));
            GUI.BeginGroup(new Rect(0f, -state.offset, content.width, content.height));
            __state = true;
        }

        internal static Exception PaneBodyFinalizer(object __instance, bool __state, Exception __exception)
        {
            if (__state)
            {
                GUI.EndGroup();
                GUI.EndGroup();
                if (__exception == null && Event.current.type == EventType.Repaint && overflow.TryGetValue(__instance, out OverflowState state))
                {
                    Rect content = paneRect(__instance);
                    // Repaint only: the source-card wheel owns scrolling, not
                    // scrollbar clicks that would change upstream card routing.
                    GUI.VerticalScrollbar(new Rect(content.width - 12f, 4f, 8f, UI.screenHeight - 8f),
                        state.offset, UI.screenHeight, 0f, content.height);
                }
            }
            return __exception;
        }

        private static OverflowState CreateOverflow(object key)
        {
            return new OverflowState();
        }

        private static bool IsStoreLocal(CodeInstruction instruction)
        {
            return instruction.opcode == OpCodes.Stloc || instruction.opcode == OpCodes.Stloc_S
                || instruction.opcode == OpCodes.Stloc_0 || instruction.opcode == OpCodes.Stloc_1
                || instruction.opcode == OpCodes.Stloc_2 || instruction.opcode == OpCodes.Stloc_3;
        }

        private static CodeInstruction LoadLocal(CodeInstruction store)
        {
            if (store.opcode == OpCodes.Stloc_0) return new CodeInstruction(OpCodes.Ldloc_0);
            if (store.opcode == OpCodes.Stloc_1) return new CodeInstruction(OpCodes.Ldloc_1);
            if (store.opcode == OpCodes.Stloc_2) return new CodeInstruction(OpCodes.Ldloc_2);
            if (store.opcode == OpCodes.Stloc_3) return new CodeInstruction(OpCodes.Ldloc_3);
            return new CodeInstruction(store.opcode == OpCodes.Stloc_S ? OpCodes.Ldloc_S : OpCodes.Ldloc, store.operand);
        }

        private static AccessTools.FieldRef<object, T> FieldRef<T>(Type type, string name)
        {
            return AccessTools.FieldRefAccess<object, T>(RequireField(type, name));
        }

        private static HarmonyMethod PatchMethod(string name)
        {
            return new HarmonyMethod(typeof(HoverPatches), name);
        }

        private static Type RequireType(string name)
        {
            return AccessTools.TypeByName(name) ?? throw new TypeLoadException(name);
        }

        private static MethodInfo RequireMethod(Type type, string name, params Type[] parameters)
        {
            return AccessTools.DeclaredMethod(type, name, parameters) ?? throw new MissingMethodException(type.FullName, name);
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
        }

        private static InvalidOperationException Unsupported(string feature)
        {
            return new InvalidOperationException("Unsupported Mission Control hover pane: " + feature + ".");
        }
    }
}
