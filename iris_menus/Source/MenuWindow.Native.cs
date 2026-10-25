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
            private readonly UiButton scope, theme, help, original, manage, back, contextButton, reset, retry;
            private readonly UiId closeId;
            private readonly UiTooltip closeTip, themeTip;
            private readonly UiPageTransition transition;
            private readonly UiDrawer navigationDrawer, contextDrawer;
            private readonly UiDialog managementDialog, resetDialog, helpDialog;
            private readonly UiScrollView railScroll, resultsScroll, managementScroll, contextScroll;
            private readonly Action<Rect> drawRail, drawResults, drawPage, drawContext, drawManagement, drawReset, drawHelp;
            private UiTween presence, closeHover, footerOriginalHover, footerHelpHover;
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
            private UiGlyphKind[] registeredIcons = Array.Empty<UiGlyphKind>(), hostedIcons = Array.Empty<UiGlyphKind>();
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
                scope = new UiButton(ui); theme = new UiButton(ui);
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
                rowHeight = (Settings.Density == 0 ? 43f : Settings.Density == 2 ? 63f : 53f) * scale;
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
                        closeTip.DrawOverlay(viewport, new Vector2(180f * scale, 44f * scale), scale);
                        themeTip.DrawOverlay(viewport, new Vector2(220f * scale, 44f * scale), scale);
                    }
                }
                finally { ui.EndFrame(); saved.Restore(); }
            }

            private static string T(string key) => key.Translate().ToString();

            private void DrawHeader()
            {
                UiPalette p = ui.Palette;
                float h = 88f * scale;
                ui.Fill(new Rect(0f, h - 1f, viewport.width, 1f), p.Line);
                float x = 36f * scale;
                if (narrow)
                {
                    if (navigationToggle.Draw(new Rect(x, 24f * scale, 40f * scale, 40f * scale), "",
                        variant: UiButtonVariant.Ghost, icon: UiGlyphKind.Menu, scale: scale)) navigationOpen = !navigationOpen;
                    x += 52f * scale;
                }
                float oldSize = ui.FontSize;
                FontStyle oldStyle = ui.FontStyle;
                ui.FontSize = 28f * scale;
                ui.FontStyle = FontStyle.Bold;
                ui.Label(new Rect(x, 16f * scale, 42f * scale, 56f * scale), "Iris", p.Ink);
                ui.FontStyle = FontStyle.Normal;
                ui.Label(new Rect(x + 42f * scale, 16f * scale, 104f * scale, 56f * scale), "Menus", p.Ink);
                ui.Fill(new Rect(x + 154f * scale, 45f * scale, 6f * scale, 6f * scale), p.Ink);
                ui.FontSize = 13f * scale;
                if (viewport.width > 1120f * scale)
                {
                    ui.Fill(new Rect(x + 188f * scale, 34f * scale, 1f, 20f * scale), p.StrongLine);
                    ui.Label(new Rect(x + 216f * scale, 24f * scale, 160f * scale, 40f * scale), T("IM_NativeModSettings"), p.Muted);
                }
                float right = viewport.width - 36f * scale;
                Rect close = new Rect(right - 40f * scale, 24f * scale, 40f * scale, 40f * scale);
                DrawClose(close);
                Rect themeRect = new Rect(close.x - 54f * scale, close.y, 40f * scale, close.height);
                if (theme.Draw(themeRect, "", variant: UiButtonVariant.Ghost,
                    icon: Settings.Theme == UiTheme.Dark ? UiGlyphKind.Sun : UiGlyphKind.Moon, hoverIconRotation: -15f, scale: scale))
                    Settings.Theme = Settings.Theme == UiTheme.Dark ? UiTheme.Light : UiTheme.Dark;
                themeTip.Delay = Settings.TipDelay;
                themeTip.Observe(themeRect, T(Settings.Theme == UiTheme.Dark ? "IM_NativeSwitchLight" : "IM_NativeSwitchDark"), theme.Id, Settings.ShowTips);
                float width = Mathf.Min(380f * scale, viewport.width * 0.23f);
                width = Mathf.Clamp(width, Mathf.Min(220f * scale, Mathf.Max(120f * scale, themeRect.x - x - 188f * scale)),
                    Mathf.Max(120f * scale, themeRect.x - x - 188f * scale));
                Rect field = new Rect(themeRect.x - width - 14f * scale, close.y, width, close.height);
                string query = search.Draw(field, host.browse.Query, T("IM_NativeSearchPlaceholder"),
                    icon: UiGlyphKind.Search, shortcut: "Ctrl K", showClear: true, scale: scale);
                if (query != host.browse.Query) ChangeQuery(query);
                ui.FontSize = oldSize;
                ui.FontStyle = oldStyle;
            }

            private void DrawClose(Rect rect)
            {
                UiInteraction hit = ui.Interact(closeId, rect);
                float h = closeHover.Step(hit.Hovered || hit.Pressed ? 1f : 0f, ui.Time, UiMotion.Fast, ui.ReducedMotion);
                UiPalette p = ui.Palette;
                ui.Fill(rect, Color.Lerp(Color.clear, p.Selected, h));
                ui.Border(rect, p.StrongLine);
                if (hit.Focused && ui.FocusVisible) ui.Border(rect.ContractedBy(2f), p.Ink, 2f);
                UiGlyph.Draw(ui, rect.ContractedBy(10f * scale), UiGlyphKind.Close, Color.Lerp(p.Ink, p.OnSelected, h));
                closeTip.Delay = Settings.TipDelay;
                closeTip.Observe(rect, T("IM_NativeClose"), closeId, Settings.ShowTips);
                if (hit.Clicked) host.Close();
            }

            private void DrawWorkspace()
            {
                float top = 116f * scale, bottom = viewport.height - 64f * scale;
                float left = 36f * scale, railWidth = 244f * scale, gap = 40f * scale;
                if (!narrow)
                {
                    Rect rail = new Rect(left, top, railWidth, Mathf.Max(1f, bottom - top));
                    DrawRailPanel(rail);
                    left += railWidth + gap;
                }
                if (pageChanged) return;
                Rect main = new Rect(left, top, Mathf.Max(1f, viewport.width - left - 36f * scale), Mathf.Max(1f, bottom - top));
                DrawBreadcrumb(main);
                main.yMin += 64f * scale;
                if (host.browse.ShowResults)
                {
                    DrawResultArea(main);
                    return;
                }
                if (host.browse.ViewingTarget && back.Draw(new Rect(main.x, main.y, Mathf.Min(200f * scale, main.width), 34f * scale),
                    T("IM_BackResults"), variant: UiButtonVariant.Text, scale: scale) && host.session.Save())
                { host.browse.Back(); host.RebuildNavigation(); }
                if (host.browse.ViewingTarget) main.yMin += 42f * scale;
                if (selected == null)
                {
                    ui.Label(main, T("IM_NativeChoosePage"), ui.Palette.Muted, TextAnchor.MiddleCenter, true);
                    return;
                }
                float oldSize = ui.FontSize;
                ui.FontSize = 13f * scale;
                DrawTabs(main);
                ui.FontSize = oldSize;
                ui.Fill(new Rect(main.x, main.y + 44f * scale, main.width, 1f), ui.Palette.StrongLine);
                if (pageChanged) return;
                main.yMin += 72f * scale;
                bool hasContext = selected.Native != null && (selected.Native.Chapters.Count > 0 || selected.Native.Notes.Count > 0 || selected.Native.HasPreview);
                bool sideContext = hasContext && main.width >= 870f * scale;
                Rect page = main;
                if (sideContext)
                {
                    float contextWidth = Mathf.Clamp(main.width * 0.26f, 220f * scale, 300f * scale);
                    page.width -= contextWidth + 32f * scale;
                }
                else if (hasContext)
                {
                    if (contextButton.Draw(new Rect(main.xMax - 108f * scale, main.y - 72f * scale, 108f * scale, 40f * scale),
                        T("IM_NativeThisPage"), variant: UiButtonVariant.Text, icon: UiGlyphKind.Layers, scale: scale)) contextOpen = true;
                }
                if (selected.Native == null)
                {
                    ui.Fill(page, UiPalette.Dark.Surface);
                    Rect content = page.ContractedBy(16f * scale);
                    transition.Draw(content, selected.Id, drawPage);
                }
                else
                {
                    ui.Fill(page, WithAlpha(ui.Palette.Sheet, Settings.NativePanelOpacity));
                    float inset = Mathf.Min(28f * scale, page.width * 0.06f);
                    Rect content = new Rect(page.x + inset, page.y, Mathf.Max(1f, page.width - inset * 2f), page.height);
                    float footer = selected.Native.CanReset ? 64f * scale : 0f;
                    content.height = Mathf.Max(1f, content.height - footer);
                    transition.Draw(content, selected.Id, drawPage);
                    if (footer > 0f)
                    {
                        Rect row = new Rect(page.x + inset, page.yMax - footer, page.width - inset * 2f, footer);
                        ui.Fill(new Rect(row.x, row.y, row.width, 1f), ui.Palette.Line);
                        float footerSize = ui.FontSize;
                        ui.FontSize = 10f * scale;
                        if (row.width > 330f * scale)
                        {
                            UiGlyph.Draw(ui, new Rect(row.x, row.y + 26f * scale, 12f * scale, 12f * scale), UiGlyphKind.Check, ui.Palette.Muted);
                            ui.Label(new Rect(row.x + 18f * scale, row.y + 16f * scale, row.width - 170f * scale, 32f * scale),
                                T("IM_NativeSavedAutomatically"), ui.Palette.Muted);
                        }
                        ui.FontSize = 11f * scale;
                        if (reset.Draw(new Rect(row.xMax - 140f * scale, row.y + 18f * scale, 140f * scale, 28f * scale), T("IM_NativeReset"),
                            variant: UiButtonVariant.Text, icon: UiGlyphKind.Reset, scale: scale))
                        { resetTarget = selected; resetOpen = true; }
                        ui.FontSize = footerSize;
                    }
                }
                if (sideContext) DrawContextPanel(new Rect(page.xMax + 32f * scale, main.y, main.xMax - page.xMax - 32f * scale, main.height));
            }

            private void DrawBreadcrumb(Rect rect)
            {
                UiPalette p = ui.Palette;
                float oldSize = ui.FontSize;
                FontStyle oldStyle = ui.FontStyle;
                ui.Fill(new Rect(rect.x, rect.y + 17f * scale, 6f * scale, 6f * scale), p.Marker);
                string title = host.browse.ShowResults ? T("IM_NativeSearchResults") : host.session.Selected?.DisplayModName ?? T("IM_Global");
                ui.FontSize = 16f * scale;
                ui.FontStyle = FontStyle.Bold;
                float titleWidth = Mathf.Min(Mathf.Max(100f * scale, Text.CalcSize(title).x + 12f * scale), rect.width * 0.65f);
                ui.Label(new Rect(rect.x + 22f * scale, rect.y, titleWidth, 40f * scale), title, p.Ink);
                float separatorX = rect.x + 22f * scale + titleWidth;
                UiGlyph.Draw(ui, new Rect(separatorX, rect.y + 13f * scale, 14f * scale, 14f * scale), UiGlyphKind.ChevronRight, p.Muted);
                ui.FontSize = 12f * scale;
                ui.FontStyle = FontStyle.Normal;
                string location = host.browse.ShowResults ? host.catalog.Results.Count.ToString() : T("IM_NativeModSettings");
                ui.Label(new Rect(separatorX + 26f * scale, rect.y, Mathf.Max(0f, rect.xMax - separatorX - 26f * scale), 40f * scale), location, p.Muted);
                ui.FontSize = oldSize;
                ui.FontStyle = oldStyle;
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
                UiPalette p = ui.Palette;
                float h = 40f * scale;
                Rect footer = new Rect(0f, viewport.height - h, viewport.width, h);
                ui.Fill(footer, p.Footer);
                ui.Fill(new Rect(footer.x, footer.y, footer.width, 1f), p.FooterLine);
                ui.Fill(new Rect(36f * scale, footer.y + 17f * scale, 5f * scale, 5f * scale), p.FooterInk);
                float oldSize = ui.FontSize;
                ui.FontSize = 11f * scale;
                ui.Label(new Rect(49f * scale, footer.y, 310f * scale, h), T("IM_NativeSaveOnLeave"), p.FooterInk);
                float helpX = viewport.width - 68f * scale;
                if (viewport.width > 900f * scale)
                {
                    Rect key = new Rect(helpX - 112f * scale, footer.y + 9f * scale, 35f * scale, 22f * scale);
                    ui.Border(key, p.FooterLine);
                    ui.Label(key, "Esc", p.FooterMuted, TextAnchor.MiddleCenter);
                    ui.Label(new Rect(key.xMax + 8f * scale, footer.y, 60f * scale, h), T("Back"), p.FooterMuted);
                }
                Rect originalRect = new Rect(helpX - (viewport.width > 900f * scale ? 324f : 212f) * scale,
                    footer.y + 4f * scale, 190f * scale, h - 8f * scale);
                UiInteraction originalHit = ui.Interact(original.Id, originalRect);
                float originalHover = footerOriginalHover.Step(originalHit.Hovered || originalHit.Pressed ? 1f : 0f, ui.Time, UiMotion.Fast, ui.ReducedMotion);
                ui.Label(originalRect, T("IM_NativeReturnOriginal"), Color.Lerp(p.FooterMuted, p.FooterInk, originalHover), TextAnchor.MiddleCenter);
                if (originalHit.Focused && ui.FocusVisible) ui.Border(originalRect, p.FooterInk);
                if (originalHit.Clicked) { returning = true; SamplePresence(); }
                Rect helpRect = new Rect(helpX, footer.y + 4f * scale, 32f * scale, h - 8f * scale);
                UiInteraction helpHit = ui.Interact(help.Id, helpRect);
                float helpHover = footerHelpHover.Step(helpHit.Hovered || helpHit.Pressed ? 1f : 0f, ui.Time, UiMotion.Fast, ui.ReducedMotion);
                ui.Fill(helpRect, WithAlpha(p.FooterLine, helpHover));
                UiGlyph.Draw(ui, helpRect.ContractedBy(8f * scale), UiGlyphKind.Help, p.FooterInk);
                if (helpHit.Focused && ui.FocusVisible) ui.Border(helpRect, p.FooterInk);
                if (helpHit.Clicked) helpOpen = true;
                ui.FontSize = oldSize;
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
