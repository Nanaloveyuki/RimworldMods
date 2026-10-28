using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using IrisUI.Framework;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace IrisUI.Inspector
{
    internal static class InspectorController
    {
        internal const int NativeWindowId = -235086;
        private static readonly AccessTools.FieldRef<InspectTabBase, Vector2> TabSize =
            AccessTools.FieldRefAccess<InspectTabBase, Vector2>("size");
        private static readonly AccessTools.FieldRef<WindowStack, List<Window>> Windows =
            AccessTools.FieldRefAccess<WindowStack, List<Window>>("windows");
        private static readonly Action<InspectTabBase, IInspectPane> ToggleTab =
            AccessTools.MethodDelegate<Action<InspectTabBase, IInspectPane>>(
                AccessTools.Method(typeof(InspectPaneUtility), "InterfaceToggleTab"));
        private static readonly List<InspectTabBase> visible = new List<InspectTabBase>();
        private static readonly List<InspectTabBase> scratch = new List<InspectTabBase>();
        private static InspectorSurface surface;
        private static Pawn lastPawn;
        private static MainTabWindow_Inspect managedPane;
        private static Game session;
        private static Type preferredTab;
        private static object language;
        private static bool managed, originalBackground, originalShadow;
        private static bool validating;
        internal static InspectTabBase ActiveTab { get; private set; }
        internal static bool RegisteringNative { get; private set; }
        internal static MainTabWindow_Inspect Pane => MainButtonDefOf.Inspect.TabWindow as MainTabWindow_Inspect;
        internal static Pawn SelectedPawn => Find.Selector.SingleSelectedThing as Pawn;
        internal static IReadOnlyList<InspectTabBase> VisibleTabs => visible;
        internal static bool IsActive => Current.ProgramState == ProgramState.Playing && Find.CurrentMap != null &&
            !WorldRendererUtility.WorldSelected && !Find.ScreenshotModeHandler.Active &&
            Find.MainTabsRoot.OpenTab == MainButtonDefOf.Inspect && Find.Selector.NumSelected == 1 && SelectedPawn != null;

        internal static InspectorLayoutRequest GetRequest()
        {
            if (!IsActive) return default;
            InspectTabBase active = FindActive(Pane);
            return new InspectorLayoutRequest(true, InspectorMod.Settings.Mode, active == null ? Vector2.zero : TabSize(active));
        }

        private static InspectTabBase FindActive(MainTabWindow_Inspect pane)
        {
            IEnumerable<InspectTabBase> tabs = pane.CurTabs;
            if (tabs == null || pane.OpenTabType == null) return null;
            if (tabs is IList list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    var tab = (InspectTabBase)list[i];
                    if (tab.GetType() == pane.OpenTabType && tab.IsVisible) return tab;
                }
            }
            else
                foreach (InspectTabBase tab in tabs)
                    if (tab.GetType() == pane.OpenTabType && tab.IsVisible) return tab;
            return null;
        }

        internal static void BeforeUpdate(MainTabWindow_Inspect pane)
        {
            if (!IsActive || pane != Pane) return;
            Pawn pawn = SelectedPawn;
            if (pawn != lastPawn)
            {
                lastPawn = pawn;
                RestoreCompactModeForSelection();
                if (preferredTab != null && pane.OpenTabType != preferredTab)
                {
                    IEnumerable<InspectTabBase> tabs = pane.CurTabs;
                    if (tabs != null)
                        foreach (InspectTabBase tab in tabs)
                            if (tab.GetType() == preferredTab && tab.IsVisible)
                            {
                                ToggleTab(tab, pane);
                                break;
                            }
                }
                surface?.RestartFade();
            }
            validating = true;
        }

        internal static void AfterUpdate() => validating = false;
        internal static void Remember(Type type)
        {
            if (!validating && IsActive) preferredTab = type;
        }

        internal static void SetMode(InspectorMode mode)
        {
            if (InspectorMod.Settings.Mode == mode) return;
            InspectorMod.Settings.Mode = mode;
            if (mode != InspectorMode.Fullscreen) InspectorMod.Settings.CompactMode = mode;
            InspectorMod.Instance.WriteSettings();
            surface?.RestartFade();
            Synchronize();
        }

        internal static void ReturnFromFullscreen() => SetMode(InspectorMod.Settings.CompactMode);

        private static void RestoreCompactModeForSelection()
        {
            if (InspectorMod.Settings.Mode != InspectorMode.Fullscreen) return;
            InspectorMod.Settings.Mode = InspectorMod.Settings.CompactMode;
            InspectorMod.Instance.WriteSettings();
            surface?.RestartFade();
        }

        internal static Rect ShellRect(UiLayout layout)
        {
            if (layout.Fullscreen || layout.Mode == InspectorMode.Minimum) return layout.Inspector;
            Rect rect = layout.Inspector;
            rect.yMin = Mathf.Min(rect.yMin, layout.Navigation.yMin);
            rect.xMax = Mathf.Max(rect.xMax, layout.Navigation.xMax);
            return rect;
        }

        internal static void Synchronize()
        {
            if (Current.ProgramState != ProgramState.Playing || session != Current.Game)
            {
                if (managedPane != null)
                {
                    managedPane.doWindowBackground = originalBackground;
                    managedPane.drawShadow = originalShadow;
                }
                managedPane = null;
                managed = validating = RegisteringNative = false;
                lastPawn = null;
                preferredTab = null;
                ActiveTab = null;
                visible.Clear(); scratch.Clear();
                language = null;
                NativeTabHost.Clear();
                surface?.ResetSession();
                session = Current.ProgramState == ProgramState.Playing ? Current.Game : null;
                if (Current.ProgramState != ProgramState.Playing) return;
            }
            if (MainButtonDefOf.Inspect == null) return;
            MainTabWindow_Inspect pane = Pane;
            if (!IsActive)
            {
                if (managed)
                {
                    pane.doWindowBackground = originalBackground;
                    pane.drawShadow = originalShadow;
                    Vector2 size = pane.InitialSize;
                    pane.windowRect = new Rect(0f, UI.screenHeight - 35f - size.y, size.x, size.y);
                    RemoveNative();
                    managed = false;
                    managedPane = null;
                    ActiveTab = null;
                    lastPawn = null;
                }
                return;
            }
            if (SelectedPawn != lastPawn) RestoreCompactModeForSelection();
            if (!managed)
            {
                originalBackground = pane.doWindowBackground;
                originalShadow = pane.drawShadow;
                managedPane = pane;
                managed = true;
                surface?.RestartFade();
            }
            pane.doWindowBackground = false;
            pane.drawShadow = false;
            ActiveTab = FindActive(pane);
            UiLayout layout = IrisLayout.Current;
            pane.windowRect = ShellRect(layout);
            bool showNative = layout.Mode != InspectorMode.Minimum && ActiveTab != null && layout.Body.width > 0f && layout.Body.height > 0f;
            ImmediateWindow native = NativeWindow();
            if (!showNative || (native != null && !NativeTabHost.Matches(ActiveTab)))
            {
                if (native != null) Find.WindowStack.TryRemove(native, false);
                NativeTabHost.Clear();
            }
            else if (native != null) native.windowRect = layout.Body;
        }

        internal static ImmediateWindow NativeWindow()
        {
            List<Window> windows = Windows(Find.WindowStack);
            for (int i = 0; i < windows.Count; i++)
                if (windows[i].ID == NativeWindowId) return windows[i] as ImmediateWindow;
            return null;
        }

        internal static void RemoveNative()
        {
            ImmediateWindow native = NativeWindow();
            if (native != null) Find.WindowStack.TryRemove(native, false);
            NativeTabHost.Clear();
        }

        internal static void AdaptTabRect(InspectTabBase tab, ref Rect rect)
        {
            if (!IsActive || tab.GetType() != Pane.OpenTabType) return;
            Rect body = IrisLayout.Current.Body;
            rect.position = body.position;
        }

        internal static void DrawTabs(MainTabWindow_Inspect pane)
        {
            RefreshTabs(pane);
            ActiveTab = FindActive(pane);
            if (InspectorMod.Settings.Mode == InspectorMode.Minimum || ActiveTab == null) return;
            RegisteringNative = true;
            try { ActiveTab.DoTabGUI(); }
            finally { RegisteringNative = false; }
            pane.RecentHeight = UI.screenHeight - IrisLayout.Current.Body.yMin;
        }

        internal static void RefreshTabs(MainTabWindow_Inspect pane)
        {
            scratch.Clear();
            IEnumerable<InspectTabBase> tabs = pane.CurTabs;
            if (tabs is IList list)
            {
                for (int i = 0; i < list.Count; i++) AddVisible((InspectTabBase)list[i]);
            }
            else if (tabs != null)
                foreach (InspectTabBase tab in tabs) AddVisible(tab);
            bool changed = language != LanguageDatabase.activeLanguage || scratch.Count != visible.Count;
            if (!changed)
                for (int i = 0; i < scratch.Count; i++)
                    if (!ReferenceEquals(scratch[i], visible[i])) { changed = true; break; }
            if (!changed) return;
            visible.Clear(); visible.AddRange(scratch);
            language = LanguageDatabase.activeLanguage;
            if (surface == null) surface = new InspectorSurface();
            surface.UpdateTabs(visible);
        }

        private static void AddVisible(InspectTabBase tab)
        {
            if (tab.IsVisible && !tab.Hidden) scratch.Add(tab);
        }

        internal static void SelectPage(int index)
        {
            MainTabWindow_Inspect pane = Pane;
            if (index == 0)
            {
                pane.CloseOpenTab();
                preferredTab = null;
            }
            else if (index <= visible.Count)
            {
                InspectTabBase tab = visible[index - 1];
                if (pane.OpenTabType == tab.GetType()) return;
                ToggleTab(tab, pane);
            }
            surface?.RestartFade();
            Synchronize();
        }

        internal static void DrawShell(Rect rect)
        {
            RefreshTabs(Pane);
            if (surface == null) { surface = new InspectorSurface(); surface.UpdateTabs(visible); }
            surface.Draw(rect, Pane, SelectedPawn);
        }

        internal static void Close()
        {
            Find.Selector.ClearSelection();
            Find.MainTabsRoot.EscapeCurrentTab();
            RemoveNative();
            Synchronize();
        }
    }
}
