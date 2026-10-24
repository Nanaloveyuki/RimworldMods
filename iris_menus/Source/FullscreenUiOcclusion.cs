using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Noise;

namespace IrisMenus
{
    internal static class FullscreenUiOcclusion
    {
        private static bool CoversScreen(Window window)
        {
            if (!(window is MenuWindow menu) || !menu.NativeFullscreen || menu.NativeOpacity <= 0f) return false;
            if ((!window.drawInScreenshotMode && Find.UIRoot.screenshotMode.Active) ||
                (window.onlyDrawInDevMode && !Prefs.DevMode)) return false;
            Rect bounds = window.windowRect;
            return bounds.xMin <= 0f && bounds.yMin <= 0f &&
                bounds.xMax >= Verse.UI.screenWidth && bounds.yMax >= Verse.UI.screenHeight;
        }

        internal static bool ShouldSuppressWindow(Window window)
        {
            WindowStack stack = Find.WindowStack;
            if (stack == null || window == null) return false;
            bool covered = false;
            for (int i = stack.Count - 1; i >= 0; i--)
            {
                Window candidate = stack[i];
                if (candidate == window) return covered;
                if (CoversScreen(candidate)) covered = true;
            }
            return false;
        }

        internal static bool ShouldSuppressHud()
        {
            WindowStack stack = Find.WindowStack;
            if (stack == null) return false;
            if (stack.currentlyDrawnWindow != null) return ShouldSuppressWindow(stack.currentlyDrawnWindow);
            for (int i = stack.Count - 1; i >= 0; i--)
                if (CoversScreen(stack[i])) return true;
            return false;
        }

        private static void DrawWindow(Window window)
        {
            if (!ShouldSuppressWindow(window)) window.WindowOnGUI();
        }

        private static void DrawExtra(Window window)
        {
            if (ShouldSuppressWindow(window)) return;
            WindowStack stack = Find.WindowStack;
            Window previous = stack.currentlyDrawnWindow;
            // ExtraOnGUI precedes the ordinary window group, but belongs to the same layer.
            stack.currentlyDrawnWindow = window;
            try { window.ExtraOnGUI(); }
            finally { stack.currentlyDrawnWindow = previous; }
        }

        private static bool DrawShadow(Window window) => window.drawShadow && !ShouldSuppressWindow(window);

        [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI))]
        private static class StackDrawing
        {
            private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                FieldInfo shadow = AccessTools.Field(typeof(Window), nameof(Window.drawShadow));
                MethodInfo extra = AccessTools.Method(typeof(Window), nameof(Window.ExtraOnGUI));
                MethodInfo draw = AccessTools.Method(typeof(Window), nameof(Window.WindowOnGUI));
                MethodInfo gatedShadow = AccessTools.Method(typeof(FullscreenUiOcclusion), nameof(DrawShadow));
                MethodInfo gatedExtra = AccessTools.Method(typeof(FullscreenUiOcclusion), nameof(DrawExtra));
                MethodInfo gatedDraw = AccessTools.Method(typeof(FullscreenUiOcclusion), nameof(DrawWindow));
                var code = new List<CodeInstruction>(instructions);
                int shadows = 0, extras = 0, windows = 0;
                foreach (CodeInstruction instruction in code)
                {
                    if (instruction.opcode == OpCodes.Ldfld && Equals(instruction.operand, shadow))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = gatedShadow;
                        shadows++;
                    }
                    else if (instruction.Calls(extra))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = gatedExtra;
                        extras++;
                    }
                    else if (instruction.Calls(draw))
                    {
                        instruction.opcode = OpCodes.Call;
                        instruction.operand = gatedDraw;
                        windows++;
                    }
                }
                if (shadows != 1 || extras != 1 || windows != 1)
                    throw new InvalidOperationException("IrisMenus fullscreen occlusion could not locate the window draw callsites.");
                // Keep immediate-window retirement and deferred ordering in the original stack.
                return code;
            }
        }

        [HarmonyPatch]
        private static class LowerHudDrawing
        {
            private static IEnumerable<MethodBase> TargetMethods()
            {
                yield return AccessTools.Method(typeof(MainMenuDrawer), nameof(MainMenuDrawer.MainMenuOnGUI));
                yield return AccessTools.Method(typeof(MapInterface), nameof(MapInterface.MapInterfaceOnGUI_BeforeMainTabs));
                yield return AccessTools.Method(typeof(MapInterface), nameof(MapInterface.MapInterfaceOnGUI_AfterMainTabs));
                yield return AccessTools.Method(typeof(WorldInterface), nameof(WorldInterface.WorldInterfaceOnGUI));
                yield return AccessTools.Method(typeof(MainButtonsRoot), nameof(MainButtonsRoot.MainButtonsOnGUI));
                yield return AccessTools.Method(typeof(AlertsReadout), nameof(AlertsReadout.AlertsReadoutOnGUI));
                yield return AccessTools.Method(typeof(NoiseDebugUI), nameof(NoiseDebugUI.NoiseDebugOnGUI));
            }

            private static bool Prefix() => !ShouldSuppressHud();
        }
    }
}
