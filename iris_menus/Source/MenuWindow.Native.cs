using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using IrisMenus.UI;

namespace IrisMenus
{
    public sealed partial class MenuWindow
    {
        private void EnterNative()
        {
            if (nativeSurface != null) return;
            legacyRect = windowRect;
            nativeSurface = new NativeSurface(this);
            doCloseX = draggable = resizeable = false;
            ClampGeometry();
            GUI.FocusControl(null);
        }

        private void LeaveNative()
        {
            nativeSurface.Dispose();
            nativeSurface = null;
            background.NativeFullscreen = false;
            doCloseX = draggable = resizeable = true;
            windowRect = legacyRect;
            ClampGeometry();
            GUI.FocusControl(null);
        }

        private sealed partial class NativeSurface : IDisposable
        {
            private readonly MenuWindow host;
            private readonly UiContext ui = new UiContext();
            private readonly NativeBackdrop backdrop = new NativeBackdrop();
            private readonly UiTextField search;
            private readonly UiButton clear, scope, theme, help, original, manage, back, contextButton, reset, retry;
            private readonly UiId closeId;
            private readonly UiTooltip closeTip, themeTip;
            private readonly UiPageTransition transition;
            private readonly UiDrawer navigationDrawer, contextDrawer;
            private readonly UiDialog managementDialog, resetDialog, helpDialog;
            private readonly UiScrollView railScroll, resultsScroll, managementScroll, contextScroll;
            private readonly Action<Rect> drawRail, drawResults, drawPage, drawContext, drawManagement, drawReset, drawHelp;
            private UiTween presence, closeRotation, closeHover;
            private bool closing, returning, navigationOpen, contextOpen, managementOpen, resetOpen, helpOpen;
            private PageState resetTarget;
            private Rect viewport;
            private float scale, rowHeight;
            private bool narrow;
            private bool pageChanged;
            private PageState selected;
            private int revision = -1;
            private object language;
            private readonly Dictionary<string, PageState> states = new Dictionary<string, PageState>(StringComparer.Ordinal);
            private readonly List<OwnerGroup> owners = new List<OwnerGroup>();
            private readonly List<OwnerGroup> registeredOwners = new List<OwnerGroup>();
            private readonly List<OwnerGroup> hostedOwners = new List<OwnerGroup>();
            private UiSideRail registeredRail, hostedRail;
            private string[] registeredLabels = new string[0], hostedLabels = new string[0];
            private readonly UiButton[] resultButtons = new UiButton[MenuCatalog.PageSize];
            private readonly UiButton previous, next, done, cancelReset, confirmReset, closeHelp, license;
            private readonly Dictionary<string, UiCheckbox> adapterChecks = new Dictionary<string, UiCheckbox>(StringComparer.Ordinal);
            private MenuSettings Settings => host.Settings;
            internal float Opacity => presence.Value;
            internal bool Closing => closing || returning;
            internal bool ReadyToClose => closing && SamplePresence() <= 0f;
            internal bool ReadyToReturn => returning && SamplePresence() <= 0f;

            internal NativeSurface(MenuWindow host)
            {
                this.host = host;
                search = new UiTextField(ui);
                clear = new UiButton(ui); scope = new UiButton(ui); theme = new UiButton(ui);
                help = new UiButton(ui); original = new UiButton(ui); manage = new UiButton(ui);
                back = new UiButton(ui); contextButton = new UiButton(ui); reset = new UiButton(ui); retry = new UiButton(ui);
                previous = new UiButton(ui); next = new UiButton(ui); done = new UiButton(ui);
                cancelReset = new UiButton(ui); confirmReset = new UiButton(ui); closeHelp = new UiButton(ui); license = new UiButton(ui);
                navigationToggle = new UiButton(ui);
                drawRailPanel = DrawRailPanel;
                drawManagementRows = DrawManagementRows;
                drawContextContents = DrawContextContents;
                closeId = ui.CreateId();
                closeTip = new UiTooltip(ui); themeTip = new UiTooltip(ui);
                transition = new UiPageTransition(ui);
                railScroll = new UiScrollView(ui); resultsScroll = new UiScrollView(ui);
                managementScroll = new UiScrollView(ui); contextScroll = new UiScrollView(ui);
                resultsScroll.SetPosition(host.resultScroll.y);
                navigationDrawer = new UiDrawer(ui); contextDrawer = new UiDrawer(ui);
                managementDialog = new UiDialog(ui); resetDialog = new UiDialog(ui); helpDialog = new UiDialog(ui);
                drawRail = DrawRail; drawResults = DrawResults; drawPage = DrawPage;
                drawContext = DrawContext; drawManagement = DrawManagement; drawReset = DrawReset; drawHelp = DrawHelp;
                for (int i = 0; i < resultButtons.Length; i++) resultButtons[i] = new UiButton(ui);
                SyncCatalog();
            }

            private float SamplePresence() => presence.Step(Closing ? 0f : 1f, Time.realtimeSinceStartup,
                Closing ? UiMotion.Exit : UiMotion.Enter, Settings.ReducedMotion);

            internal void PrepareWindow()
            {
                viewport = new Rect(0f, 0f, host.windowRect.width, host.windowRect.height);
                scale = Settings.NativeScale;
                rowHeight = (Settings.Density == 0 ? 42f : Settings.Density == 2 ? 60f : 52f) * scale;
                narrow = viewport.width < 880f * scale;
                SamplePresence();
            }

            internal void BeginClose() { closing = true; SamplePresence(); }

            internal void Draw(Rect rect)
            {
                var saved = new GuiState();
                try
                {
                    ui.BeginFrame(viewport, Settings.Theme, Settings.ReducedMotion);
                    ui.FontSize = 16f * scale;
                    SyncCatalog();
                    pageChanged = false;
                    selected = GetPage(host.session.Selected);
                    if (resetOpen && !ReferenceEquals(resetTarget, selected)) { resetOpen = false; resetTarget = null; }
                    if (!host.browse.ShowResults)
                    {
                        if (selected?.Native != null && !selected.Failed)
                        {
                            try { selected.Native.Prepare(); }
                            catch (ExitGUIException) { throw; }
                            catch (Exception exception) { selected.Failed = true; ReportFailure("prepare native " + selected.Page.Id, exception); }
                        }
                        selected?.TabSelect?.Prepare();
                    }
                    navigationDrawer.Prepare(navigationOpen);
                    contextDrawer.Prepare(contextOpen);
                    managementDialog.Prepare(managementOpen);
                    resetDialog.Prepare(resetOpen);
                    helpDialog.Prepare(helpOpen);
                    using (Closing || !Find.WindowStack.GetsInput(host) ? ui.Input(false) : default(UiContext.UiScope))
                    using (ui.Opacity(Opacity))
                    {
                        backdrop.Draw(ui, viewport);
                        DrawHeader();
                        DrawWorkspace();
                        DrawFooter();
                        if (!pageChanged && !host.browse.ShowResults && selected?.Native != null && !selected.Failed)
                        {
                            try { selected.Native.DrawOverlay(viewport); }
                            catch (ExitGUIException) { throw; }
                            catch (Exception exception) { selected.Failed = true; ReportFailure("overlay native " + selected.Page.Id, exception); }
                        }
                        if (!pageChanged && !host.browse.ShowResults && selected?.TabSelect != null)
                        {
                            int tab = selected.TabSelect.DrawPopup(viewport, selected.TabIndex);
                            if (tab != selected.TabIndex) SelectTab(tab);
                        }
                        DrawOverlays();
                        HandleKeys();
                        closeTip.DrawOverlay(viewport, new Vector2(180f * scale, 44f * scale));
                        themeTip.DrawOverlay(viewport, new Vector2(220f * scale, 44f * scale));
                    }
                }
                finally { ui.EndFrame(); saved.Restore(); }
            }

            private static string T(string key) => key.Translate().ToString();

            private void DrawHeader()
            {
                UiPalette p = ui.Palette;
                float h = 84f * scale;
                ui.Fill(new Rect(0f, h - 1f, viewport.width, 1f), p.Line);
                float x = 32f * scale;
                if (narrow)
                {
                    if (navigationToggle.Draw(new Rect(x, 22f * scale, 44f * scale, 40f * scale), "≡")) navigationOpen = !navigationOpen;
                    x += 54f * scale;
                }
                float oldSize = ui.FontSize;
                ui.FontSize = 27f * scale;
                ui.Label(new Rect(x, 14f * scale, 168f * scale, 56f * scale), "IrisMenus", p.Ink);
                ui.FontSize = oldSize;
                if (viewport.width > 1120f * scale)
                {
                    ui.Fill(new Rect(x + 184f * scale, 32f * scale, 1f, 20f * scale), p.StrongLine);
                    ui.Label(new Rect(x + 206f * scale, 22f * scale, 160f * scale, 40f * scale), T("IM_NativeModSettings"), p.Muted);
                }
                float right = viewport.width - 32f * scale;
                Rect close = new Rect(right - 44f * scale, 22f * scale, 44f * scale, 40f * scale);
                DrawClose(close);
                Rect themeRect = new Rect(close.x - 54f * scale, close.y, 44f * scale, close.height);
                if (theme.Draw(themeRect, Settings.Theme == UiTheme.Dark ? "☼" : "◐"))
                    Settings.Theme = Settings.Theme == UiTheme.Dark ? UiTheme.Light : UiTheme.Dark;
                themeTip.Delay = Settings.TipDelay;
                themeTip.Observe(themeRect, T(Settings.Theme == UiTheme.Dark ? "IM_NativeSwitchLight" : "IM_NativeSwitchDark"), theme.Id, Settings.ShowTips);
                float width = Mathf.Min(330f * scale, Mathf.Max(140f, themeRect.x - x - 196f * scale));
                Rect field = new Rect(themeRect.x - width - 16f * scale, close.y, width, close.height);
                string query = search.Draw(field, host.browse.Query, T("IM_NativeSearchPlaceholder"));
                if (query != host.browse.Query) ChangeQuery(query);
                if (query.Length > 0 && clear.Draw(new Rect(field.x - 42f * scale, field.y, 36f * scale, field.height), "×")) ChangeQuery("");
            }

            private void DrawClose(Rect rect)
            {
                UiInteraction hit = ui.Interact(closeId, rect);
                float h = closeHover.Step(hit.Hovered || hit.Pressed ? 1f : 0f, ui.Time, UiMotion.Fast, ui.ReducedMotion);
                float angle = closeRotation.Step(hit.Hovered ? 135f : 0f, ui.Time, UiMotion.Standard, ui.ReducedMotion) * Mathf.Deg2Rad;
                UiPalette p = ui.Palette;
                ui.Fill(rect, Color.Lerp(Color.clear, p.Selected, h));
                ui.Border(rect, p.StrongLine);
                if (hit.Focused && ui.FocusVisible) ui.Border(rect.ContractedBy(2f), p.Signal, 2f);
                Color ink = Color.Lerp(p.Ink, p.OnSelected, h);
                float c = Mathf.Cos(angle), s = Mathf.Sin(angle), r = 8f * scale;
                Vector2 a = new Vector2((-r * c + r * s), (-r * s - r * c));
                Vector2 b = new Vector2((-r * c - r * s), (-r * s + r * c));
                ui.Line(rect.center + a, rect.center - a, ink, 1.6f * scale);
                ui.Line(rect.center + b, rect.center - b, ink, 1.6f * scale);
                closeTip.Delay = Settings.TipDelay;
                closeTip.Observe(rect, T("IM_NativeClose"), closeId, Settings.ShowTips);
                if (hit.Clicked) host.Close();
            }

            private void DrawWorkspace()
            {
                float top = 112f * scale, bottom = viewport.height - 64f * scale;
                float left = 32f * scale, railWidth = 244f * scale, gap = 36f * scale;
                if (!narrow)
                {
                    Rect rail = new Rect(left, top, railWidth, Mathf.Max(1f, bottom - top));
                    DrawRailPanel(rail);
                    left += railWidth + gap;
                }
                if (pageChanged) return;
                Rect main = new Rect(left, top, Mathf.Max(1f, viewport.width - left - 32f * scale), Mathf.Max(1f, bottom - top));
                ui.Fill(new Rect(main.x, main.y + 14f * scale, 6f * scale, 6f * scale), ui.Palette.Muted);
                string title = host.browse.ShowResults ? T("IM_NativeSearchResults") : host.session.Selected?.DisplayModName ?? T("IM_Global");
                ui.Label(new Rect(main.x + 20f * scale, main.y, main.width - 20f * scale, 36f * scale), title, ui.Palette.Ink);
                main.yMin += 56f * scale;
                if (host.browse.ShowResults)
                {
                    DrawResultArea(main);
                    return;
                }
                if (host.browse.ViewingTarget && back.Draw(new Rect(main.x, main.y, Mathf.Min(200f * scale, main.width), 34f * scale), T("IM_BackResults")) && host.session.Save())
                { host.browse.Back(); host.RebuildNavigation(); }
                if (host.browse.ViewingTarget) main.yMin += 42f * scale;
                if (selected == null)
                {
                    ui.Label(main, T("IM_NativeChoosePage"), ui.Palette.Muted, TextAnchor.MiddleCenter, true);
                    return;
                }
                DrawTabs(main);
                if (pageChanged) return;
                main.yMin += 68f * scale;
                bool hasContext = selected.Native != null && (selected.Native.Chapters.Count > 0 || selected.Native.Notes.Count > 0 || selected.Native.HasPreview);
                bool sideContext = hasContext && main.width >= 870f * scale;
                Rect page = main;
                if (sideContext)
                {
                    float contextWidth = Mathf.Clamp(main.width * 0.26f, 220f * scale, 300f * scale);
                    page.width -= contextWidth + 32f * scale;
                    DrawContextPanel(new Rect(page.xMax + 32f * scale, main.y, contextWidth, main.height));
                }
                else if (hasContext)
                {
                    if (contextButton.Draw(new Rect(main.xMax - 92f * scale, main.y - 60f * scale, 92f * scale, 40f * scale), T("IM_NativeThisPage"))) contextOpen = true;
                }
                if (selected.Native == null)
                {
                    ui.Fill(page, UiPalette.Dark.Surface);
                    Rect content = page.ContractedBy(16f * scale);
                    transition.Draw(content, selected.Id, drawPage);
                }
                else
                {
                    ui.Fill(page, WithAlpha(ui.Palette.Raised, Settings.NativePanelOpacity));
                    Rect content = page.ContractedBy(20f * scale);
                    float footer = selected.Native.CanReset ? 52f * scale : 0f;
                    content.height = Mathf.Max(1f, content.height - footer);
                    transition.Draw(content, selected.Id, drawPage);
                    if (footer > 0f)
                    {
                        Rect row = new Rect(page.x + 20f * scale, page.yMax - footer, page.width - 40f * scale, footer);
                        ui.Fill(new Rect(row.x, row.y, row.width, 1f), ui.Palette.Line);
                        if (reset.Draw(new Rect(row.xMax - 140f * scale, row.y + 8f * scale, 140f * scale, 34f * scale), T("IM_NativeReset")))
                        { resetTarget = selected; resetOpen = true; }
                    }
                }
            }

            private void DrawPage(Rect rect)
            {
                if (selected == null) return;
                if (selected.Failed)
                {
                    ui.Label(new Rect(0f, 0f, rect.width, 64f * scale), T("IM_PageFailed"), ui.Palette.Error, TextAnchor.MiddleLeft, true);
                    if (retry.Draw(new Rect(0f, 72f * scale, 160f * scale, 36f * scale), T("IM_Retry")))
                    {
                        if (selected.Native == null) selected.CreateNative();
                        else selected.Failed = false;
                    }
                    return;
                }
                if (selected.Native == null)
                {
                    var saved = new GuiState();
                    try
                    {
                        GUI.color = GUI.contentColor = Color.white;
                        GUI.backgroundColor = Color.white;
                        Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                        Event current = Event.current;
                        EventType type = current != null ? current.type : EventType.Ignore;
                        bool blocked = !ui.IsInputAllowed;
                        GUI.enabled &= !blocked;
                        GUI.color = new Color(1f, 1f, 1f, ui.Alpha);
                        if (blocked && current != null && type != EventType.Layout && type != EventType.Repaint) current.type = EventType.Ignore;
                        try { host.DrawSelected(rect); }
                        finally { if (blocked && current != null && current.type == EventType.Ignore) current.type = type; }
                    }
                    finally { saved.Restore(); }
                    return;
                }
                try
                {
                    float actionsHeight = DrawNativeActions(rect);
                    Rect content = new Rect(rect.x, rect.y + actionsHeight, rect.width, Mathf.Max(1f, rect.height - actionsHeight));
                    using (ui.Clip(content)) selected.Native.Draw(new Rect(0f, 0f, content.width, content.height));
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { selected.Failed = true; ReportFailure("draw native " + selected.Page.Id, exception); }
            }

            private static Color WithAlpha(Color color, float alpha) { color.a *= alpha; return color; }

            private void DrawFooter()
            {
                float h = 40f * scale;
                Rect footer = new Rect(0f, viewport.height - h, viewport.width, h);
                ui.Fill(footer, ui.Palette.Footer);
                ui.Fill(new Rect(32f * scale, footer.y + 16f * scale, 6f * scale, 6f * scale), ui.Palette.FooterInk);
                ui.Label(new Rect(48f * scale, footer.y, 260f * scale, h), T("IM_NativeSaveOnLeave"), ui.Palette.FooterInk);
                if (original.Draw(new Rect(viewport.width - 300f * scale, footer.y + 4f * scale, 190f * scale, h - 8f * scale), T("IM_NativeReturnOriginal")))
                { returning = true; SamplePresence(); }
                if (help.Draw(new Rect(viewport.width - 76f * scale, footer.y + 4f * scale, 44f * scale, h - 8f * scale), "?")) helpOpen = true;
            }

            private void HandleKeys()
            {
                if (!ui.IsInputAllowed || Closing) return;
                Event current = ui.Event;
                if (current != null && current.type == EventType.KeyDown && (current.control || current.command) && current.keyCode == KeyCode.K)
                { ui.Focus(search.Id); current.Use(); }
                if (!ui.ConsumeKey(KeyCode.Escape)) return;
                if (host.browse.Query.Length > 0) { ChangeQuery(""); ui.Focus(default(UiId)); GUI.FocusControl(null); }
                else if (navigationOpen) navigationOpen = false;
                else host.Close();
            }

            public void Dispose()
            {
                foreach (PageState page in states.Values) page.Dispose();
                states.Clear();
                search.Dispose();
                backdrop.Dispose();
                ui.Dispose();
            }
        }
    }
}
