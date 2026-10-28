using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using IrisUI.Framework;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisUI.Inspector
{
    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.DoWindowContents))]
    internal static class InspectorContentsPatch
    {
        private static bool Prefix(Rect inRect)
        {
            if (!InspectorController.IsActive) return true;
            InspectorController.DrawShell(inRect);
            return false;
        }
    }

    [HarmonyPatch(typeof(InspectPaneUtility), "DoTabs")]
    internal static class InspectorNavigationPatch
    {
        private static bool Prefix(IInspectPane pane)
        {
            if (!InspectorController.IsActive || pane != InspectorController.Pane) return true;
            InspectorController.DrawTabs((MainTabWindow_Inspect)pane);
            return false;
        }
    }

    [HarmonyPatch(typeof(InspectTabBase), "get_TabRect")]
    internal static class InspectorTabRectPatch
    {
        private static void Postfix(InspectTabBase __instance, ref Rect __result) =>
            InspectorController.AdaptTabRect(__instance, ref __result);
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.ImmediateWindow))]
    internal static class InspectorNativeHostPatch
    {
        private static void Prefix(int ID, ref Rect rect, ref Action doWindowFunc)
        {
            if (Math.Abs(ID) != -InspectorController.NativeWindowId || !InspectorController.RegisteringNative ||
                Event.current.type != EventType.Repaint) return;
            NativeTabHost.Register(ref rect, ref doWindowFunc);
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.WindowUpdate))]
    internal static class InspectorUpdatePatch
    {
        private static void Prefix(MainTabWindow_Inspect __instance) => InspectorController.BeforeUpdate(__instance);
        private static Exception Finalizer(Exception __exception)
        {
            InspectorController.AfterUpdate();
            return __exception;
        }
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), "set_OpenTabType")]
    internal static class InspectorOpenTabPatch
    {
        private static void Postfix(Type value) => InspectorController.Remember(value);
    }

    [HarmonyPatch(typeof(MainTabWindow_Inspect), nameof(MainTabWindow_Inspect.CloseOpenTab))]
    internal static class InspectorCloseTabPatch
    {
        private static void Postfix() => InspectorController.Remember(null);
    }

    [HarmonyPatch]
    internal static class InspectorGeometryPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI));
            yield return AccessTools.Method(typeof(WindowStack), nameof(WindowStack.HandleEventsHighPriority));
            yield return AccessTools.Method(typeof(WindowStack), nameof(WindowStack.WindowStackOnGUI));
        }
        private static void Prefix() => InspectorController.Synchronize();
    }

    [HarmonyPatch(typeof(Window), nameof(Window.WindowOnGUI))]
    internal static class InspectorWindowGeometryPatch
    {
        private static void Prefix(Window __instance)
        {
            if (__instance is MainTabWindow_Inspect || __instance.ID == InspectorController.NativeWindowId)
                InspectorController.Synchronize();
        }
    }

    [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Notify_PressedCancel))]
    internal static class InspectorFullscreenEscapePatch
    {
        private static bool Prefix(WindowStack __instance)
        {
            if (!InspectorController.IsActive || InspectorMod.Settings.Mode != InspectorMode.Fullscreen ||
                __instance.NonImmediateDialogWindowOpen || !__instance.GetsInput(InspectorController.Pane)) return true;
            InspectorController.ReturnFromFullscreen();
            if (Event.current.type != EventType.Used) Event.current.Use();
            return false;
        }
    }
}
