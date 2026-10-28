using System.Collections.Generic;
using HarmonyLib;
using IrisMenus;
using IrisMenus.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisUI.Framework
{
    public sealed class InspectorCover : Window, IFullscreenUiSurface
    {
        private static readonly AccessTools.FieldRef<WindowStack, List<Window>> Windows =
            AccessTools.FieldRefAccess<WindowStack, List<Window>>("windows");
        private static readonly AccessTools.FieldRef<WindowStack, bool> OrderChanged =
            AccessTools.FieldRefAccess<WindowStack, bool>("updateInternalWindowsOrderLater");
        public bool CoversScreen => IrisLayout.Current.Fullscreen;
        protected override float Margin => 0f;
        public override Vector2 InitialSize => new Vector2(UI.screenWidth, UI.screenHeight);
        public InspectorCover()
        {
            layer = WindowLayer.GameUI;
            doWindowBackground = false; drawShadow = false;
            closeOnAccept = closeOnCancel = closeOnClickedOutside = false;
            focusWhenOpened = false; absorbInputAroundWindow = true;
            preventCameraMotion = true; drawInScreenshotMode = false;
            soundAppear = soundClose = null;
        }
        public override void DoWindowContents(Rect rect)
        {
            Color background = IrisLayout.Theme == UiTheme.Dark ? UiPalette.Dark.Surface : UiPalette.Light.Surface;
            Widgets.DrawBoxSolid(rect, background);
        }
        public override void Notify_ResolutionChanged() => windowRect = IrisLayout.Current.Viewport;
        internal static void Synchronize()
        {
            WindowStack stack = Find.WindowStack;
            InspectorCover cover = stack.WindowOfType<InspectorCover>();
            if (!IrisLayout.Current.Fullscreen)
            {
                if (cover != null) stack.TryRemove(cover, false);
                return;
            }
            if (cover == null) { cover = new InspectorCover(); stack.Add(cover); }
            cover.windowRect = IrisLayout.Current.Viewport;
            List<Window> windows = Windows(stack);
            Window inspect = MainButtonDefOf.Inspect.TabWindow;
            int index = windows.IndexOf(inspect);
            int coverIndex = windows.IndexOf(cover);
            if (index >= 0 && coverIndex != index - 1)
            {
                windows.RemoveAt(coverIndex);
                index = windows.IndexOf(inspect);
                windows.Insert(index, cover);
                OrderChanged(stack) = true;
            }
            Window nativeBody = null;
            for (int i = 0; i < windows.Count; i++)
                if (windows[i].ID == -235086) { nativeBody = windows[i]; break; }
            if (nativeBody != null && windows.IndexOf(nativeBody) < windows.IndexOf(inspect))
            {
                windows.Remove(nativeBody);
                windows.Insert(windows.IndexOf(inspect) + 1, nativeBody);
                OrderChanged(stack) = true;
            }
        }
        [HarmonyPatch(typeof(UIRoot_Play), nameof(UIRoot_Play.UIRootOnGUI))]
        private static class RefreshCover
        {
            private static void Prefix() => Synchronize();
        }
        [HarmonyPatch(typeof(WindowStack), nameof(WindowStack.Notify_ClickedInsideWindow))]
        private static class PreserveBodyOrder
        {
            private static void Postfix() => Synchronize();
        }
    }
}
