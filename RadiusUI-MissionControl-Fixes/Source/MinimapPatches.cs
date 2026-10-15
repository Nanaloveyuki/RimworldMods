using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace RadiusUI.MissionControl.Fixes
{
    internal static class MinimapPatches
    {
        private const float RailWidth = 56f;

        private static AccessTools.FieldRef<object> settings;
        private static AccessTools.FieldRef<object, float> savedX;
        private static AccessTools.FieldRef<object, float> savedY;
        private static AccessTools.FieldRef<object, float> savedSize;
        private static AccessTools.FieldRef<Window, bool> resizeLater;
        private static AccessTools.FieldRef<Window, Rect> resizeLaterRect;
        private static MethodInfo writeSettings;

        // MinimapUI owns one floating window. Tracking that instance avoids a
        // dictionary, per-window state allocations, and work on unrelated windows.
        private static Window trackedWindow;
        private static Rect observedRect;
        private static bool geometryChanged;
        private static bool mouseReleased;

        internal struct GuiEventState
        {
            internal EventType Type;
            internal int Button;
        }

        internal static void Install(Harmony harmony)
        {
            Type minimap = RequireType("RadiusUI.MissionControl.MinimapWindow");
            Type configuration = RequireType("RadiusUI.MissionControl.StyledWarningsSettings");
            Type mod = RequireType("RadiusUI.MissionControl.StyledWarningsMod");
            if (!typeof(Window).IsAssignableFrom(minimap) || !typeof(ModSettings).IsAssignableFrom(configuration))
                throw new InvalidOperationException("Unsupported Mission Control minimap/settings base types.");

            FieldInfo x = RequireField(configuration, "mmWinX");
            FieldInfo y = RequireField(configuration, "mmWinY");
            FieldInfo size = RequireField(configuration, "mmWinSize");
            FieldInfo pending = RequireField(typeof(Window), "resizeLater");
            FieldInfo pendingRect = RequireField(typeof(Window), "resizeLaterRect");
            settings = AccessTools.StaticFieldRefAccess<object>(RequireField(mod, "Settings"));
            savedX = AccessTools.FieldRefAccess<object, float>(x);
            savedY = AccessTools.FieldRefAccess<object, float>(y);
            savedSize = AccessTools.FieldRefAccess<object, float>(size);
            resizeLater = AccessTools.FieldRefAccess<Window, bool>(pending);
            resizeLaterRect = AccessTools.FieldRefAccess<Window, Rect>(pendingRect);
            writeSettings = RequireMethod(typeof(ModSettings), nameof(ModSettings.Write), Type.EmptyTypes);

            MethodInfo initialize = RequireMethod(minimap, "SetInitialSizeAndPosition", Type.EmptyTypes);
            MethodInfo contents = RequireMethod(minimap, "DoWindowContents", new[] { typeof(Rect) });
            MethodInfo close = RequireMethod(minimap, "PostClose", Type.EmptyTypes);
            MethodInfo onGUI = RequireMethod(typeof(Window), nameof(Window.WindowOnGUI), Type.EmptyTypes);
            MethodInfo innerGUI = RequireMethod(typeof(Window), "InnerWindowOnGUI", new[] { typeof(int) });
            if (AccessTools.Method(minimap, nameof(Window.WindowOnGUI), Type.EmptyTypes).DeclaringType != typeof(Window))
                throw new InvalidOperationException("Unsupported Mission Control minimap GUI override.");

            // Validate the original lifecycle before any patch is installed. In
            // particular, DoWindowContents runs before GUI.Window returns the final
            // screen-coordinate rect, and MouseUp can leave a deferred resize.
            ValidateWindowLifecycle(onGUI, innerGUI, pending, pendingRect);
            List<CodeInstruction> initialCode = new List<CodeInstruction>(PatchProcessor.GetOriginalInstructions(initialize));
            RequireFieldAccess(initialCode, OpCodes.Ldfld, x, 1, initialize.Name);
            RequireFieldAccess(initialCode, OpCodes.Ldfld, y, 1, initialize.Name);
            RequireFieldAccess(initialCode, OpCodes.Stfld, RequireField(typeof(Window), nameof(Window.windowRect)), 1, initialize.Name);
            List<CodeInstruction> closeCode = new List<CodeInstruction>(PatchProcessor.GetOriginalInstructions(close));
            RequireFieldAccess(closeCode, OpCodes.Stfld, x, 1, close.Name);
            RequireFieldAccess(closeCode, OpCodes.Stfld, y, 1, close.Name);
            RequireFieldAccess(closeCode, OpCodes.Stfld, size, 1, close.Name);
            SettingsWriteTranspiler(closeCode, close);
            SettingsWriteTranspiler(PatchProcessor.GetOriginalInstructions(contents), contents);

            harmony.Patch(initialize, postfix: PatchMethod(nameof(InitializeWindowPostfix)));
            harmony.Patch(contents, transpiler: PatchMethod(nameof(SettingsWriteTranspiler)));
            harmony.Patch(close, postfix: PatchMethod(nameof(PostClosePostfix)), transpiler: PatchMethod(nameof(SettingsWriteTranspiler)));
            harmony.Patch(onGUI, prefix: PatchMethod(nameof(WindowOnGUIPrefix)), postfix: PatchMethod(nameof(WindowOnGUIPostfix)));
        }

        internal static void InitializeWindowPostfix(Window __instance)
        {
            trackedWindow = __instance;
            observedRect = __instance.windowRect;
            geometryChanged = false;
            mouseReleased = true;
        }

        internal static void WindowOnGUIPrefix(Window __instance, out GuiEventState __state)
        {
            __state = default(GuiEventState);
            if (!ReferenceEquals(__instance, trackedWindow))
                return;
            Event current = Event.current;
            __state.Type = current.rawType;
            __state.Button = current.button;
        }

        internal static void WindowOnGUIPostfix(Window __instance, GuiEventState __state)
        {
            if (ReferenceEquals(__instance, trackedWindow))
                CompleteWindowGUI(__instance, __state.Type, __state.Button);
        }

        internal static void CompleteWindowGUI(Window window, EventType eventType, int button)
        {
            if (!ReferenceEquals(window, trackedWindow))
                return;
            if (button == 0)
            {
                if (eventType == EventType.MouseDown)
                    mouseReleased = false;
                else if (eventType == EventType.MouseUp)
                    mouseReleased = true;
            }

            Rect rect = GeometryRect(window);
            if (rect != observedRect)
            {
                observedRect = rect;
                geometryChanged = true;
            }
            if (!geometryChanged || !mouseReleased || resizeLater(window))
                return;

            ModSettings currentSettings = (ModSettings)settings();
            if (CaptureGeometry(currentSettings, window))
                currentSettings.Write();
            geometryChanged = false;
        }

        internal static void CaptureAndWrite(ModSettings currentSettings, Window window)
        {
            // Reuse each original lock/hide/dock/close write rather than adding a
            // second disk write. Capture even when locked; PostClose skips its own
            // geometry assignments in that state.
            CaptureGeometry(currentSettings, window);
            currentSettings.Write();
            if (ReferenceEquals(window, trackedWindow))
            {
                observedRect = GeometryRect(window);
                geometryChanged = false;
            }
        }

        internal static void PostClosePostfix(Window __instance)
        {
            if (ReferenceEquals(__instance, trackedWindow))
            {
                trackedWindow = null;
                geometryChanged = false;
            }
        }

        private static Rect GeometryRect(Window window)
        {
            Rect rect = window.windowRect;
            if (resizeLater(window))
            {
                // A pending resize supplies size only. Its x/y predate the
                // GUI.Window drag result and must not overwrite screen position.
                Rect pending = resizeLaterRect(window);
                rect.width = pending.width;
                rect.height = pending.height;
            }
            return rect;
        }

        private static bool CaptureGeometry(object currentSettings, Window window)
        {
            Rect rect = GeometryRect(window);
            float size = rect.width - RailWidth;
            if (savedX(currentSettings) == rect.x && savedY(currentSettings) == rect.y && savedSize(currentSettings) == size)
                return false;
            savedX(currentSettings) = rect.x;
            savedY(currentSettings) = rect.y;
            savedSize(currentSettings) = size;
            return true;
        }

        internal static IEnumerable<CodeInstruction> SettingsWriteTranspiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(instructions);
            int expected = __originalMethod.Name == "PostClose" ? 2 : 3;
            int matches = 0;
            MethodInfo replacement = AccessTools.Method(typeof(MinimapPatches), nameof(CaptureAndWrite));
            for (int i = 0; i < code.Count; i++)
            {
                if (!code[i].Calls(writeSettings))
                    continue;
                CodeInstruction loadWindow = new CodeInstruction(OpCodes.Ldarg_0);
                loadWindow.labels.AddRange(code[i].labels);
                loadWindow.blocks.AddRange(code[i].blocks);
                code[i].labels.Clear();
                code[i].blocks.Clear();
                code[i].opcode = OpCodes.Call;
                code[i].operand = replacement;
                code.Insert(i++, loadWindow);
                matches++;
            }
            if (matches != expected)
                throw new InvalidOperationException("Unsupported Mission Control minimap " + __originalMethod.Name + ": expected " + expected + " settings writes.");
            return code;
        }

        private static void ValidateWindowLifecycle(MethodInfo onGUI, MethodInfo innerGUI, FieldInfo pending, FieldInfo pendingRect)
        {
            List<CodeInstruction> code = new List<CodeInstruction>(PatchProcessor.GetOriginalInstructions(onGUI));
            MethodInfo guiWindow = AccessTools.Method(typeof(GUI), nameof(GUI.Window),
                new[] { typeof(int), typeof(Rect), typeof(GUI.WindowFunction), typeof(string), typeof(GUIStyle) });
            FieldInfo windowRect = RequireField(typeof(Window), nameof(Window.windowRect));
            int calls = 0;
            for (int i = 0; i < code.Count; i++)
            {
                if (!code[i].Calls(guiWindow))
                    continue;
                calls++;
                if (i + 1 >= code.Count || code[i + 1].opcode != OpCodes.Stfld || !Equals(code[i + 1].operand, windowRect))
                    throw new InvalidOperationException("Unsupported RimWorld GUI.Window rect assignment.");
            }
            if (calls != 1)
                throw new InvalidOperationException("Unsupported RimWorld WindowOnGUI: expected one GUI.Window call.");
            RequireFieldAccess(code, OpCodes.Ldfld, pending, 1, onGUI.Name);
            RequireFieldAccess(code, OpCodes.Ldfld, pendingRect, 1, onGUI.Name);
            List<CodeInstruction> innerCode = new List<CodeInstruction>(PatchProcessor.GetOriginalInstructions(innerGUI));
            RequireFieldAccess(innerCode, OpCodes.Stfld, pending, 1, innerGUI.Name);
            RequireFieldAccess(innerCode, OpCodes.Stfld, pendingRect, 1, innerGUI.Name);
        }

        private static void RequireFieldAccess(List<CodeInstruction> code, OpCode opcode, FieldInfo field, int expected, string method)
        {
            int matches = 0;
            foreach (CodeInstruction instruction in code)
                if (instruction.opcode == opcode && Equals(instruction.operand, field))
                    matches++;
            if (matches != expected)
                throw new InvalidOperationException("Unsupported minimap lifecycle " + method + ": unexpected " + field.Name + " accesses.");
        }

        private static HarmonyMethod PatchMethod(string name)
        {
            return new HarmonyMethod(typeof(MinimapPatches), name);
        }

        private static Type RequireType(string name)
        {
            return AccessTools.TypeByName(name) ?? throw new TypeLoadException(name);
        }

        private static MethodInfo RequireMethod(Type type, string name, Type[] parameters)
        {
            MethodInfo method = AccessTools.DeclaredMethod(type, name, parameters) ?? throw new MissingMethodException(type.FullName, name);
            if (method.IsStatic || method.ReturnType != typeof(void))
                throw new InvalidOperationException("Unsupported minimap lifecycle signature: " + type.FullName + "." + name);
            return method;
        }

        private static FieldInfo RequireField(Type type, string name)
        {
            return AccessTools.Field(type, name) ?? throw new MissingFieldException(type.FullName, name);
        }
    }
}
