using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using IrisMenus.UI;

namespace IrisMenus
{
    public sealed partial class MenuWindow
    {
#if RIMWORLD_1_7
        internal static void OpenMainButtons()
        {
            WindowStack stack = Find.WindowStack;
            MenuWindow window = stack.WindowOfType<MenuWindow>();
            if (window != null && window.nativeSurface.Closing) return;
            bool created = window == null;
            if (created) window = new MenuWindow();
            if (!window.session.Save()) return;
            if (created) stack.Add(window);
            window.nativeSurface.OpenMainButtons();
            stack.Notify_ManuallySetFocus(window);
        }
#endif


        private sealed partial class NativeSurface : IDisposable
        {
            private readonly MenuWindow host;
            private readonly UiContext ui = new UiContext();
            private readonly NativeBackdrop backdrop = new NativeBackdrop();
            private readonly NativeWindowTransition windowTransition = new NativeWindowTransition();
            private readonly Action drawFrame;
            private readonly UiTextField search;
            private readonly UiToast toast;
            private readonly Dictionary<ModContentPack, NativeModMetadata> metadata = new Dictionary<ModContentPack, NativeModMetadata>();
            private ModContentPack footerOwner;
            private string footerName, footerAuthor, footerVersion, footerDisplay;
            private float footerAvailable = -1f, footerFont;
            private object footerLanguage;
            private bool editPending;
            private double lastEdit;
            private string pendingToast;
            private UiToastTone pendingTone;
            private readonly UiButton scope, theme, help, manage, back, contextButton, reset, retry;
            private readonly UiLoader searchLoader;
            private UiToolbar toolbar;
            private object toolbarLanguage;
            private readonly HostedGuiTheme hostedTheme = new HostedGuiTheme();
            private readonly UiId closeId;
            private readonly UiTooltip closeTip, themeTip;
            private readonly UiPageTransition transition;
            private readonly UiDrawer navigationDrawer, contextDrawer;
            private readonly UiDialog resetDialog, helpDialog;
            private readonly UiScrollView railScroll, resultsScroll, managementScroll, contextScroll;
            private readonly Action<Rect> drawRail, drawResults, drawPage, drawContext, drawReset, drawHelp;
            private UiTween closeHover, footerHelpHover;
            private bool closing, navigationOpen, contextOpen, managementOpen, resetOpen, helpOpen;
            private PageState resetTarget;
            private Rect viewport;
#if RIMWORLD_1_7
            private MainButtonsPage mainButtonsPage;
            private object mainButtonsLanguage;
            private bool mainButtonsOpen;
            private string mainButtonsTitle, mainButtonsBack;
            private readonly UiId mainButtonsId;
            private readonly UiButton mainButtonsBackButton;
            private readonly Action<Rect> drawMainButtons;
            private bool MainButtonsOpen => mainButtonsOpen;
#else
            private bool MainButtonsOpen => false;
#endif
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
            private readonly UiButton[] resultButtons = new UiButton[MenuCatalog.PageSize];
            private readonly UiButton previous, next, done, cancelReset, confirmReset, closeHelp;
            private readonly UiTooltip breadcrumbTip;
            private string breadcrumbTitle, breadcrumbLocation, breadcrumbDisplay;
            private float breadcrumbAvailable = -1f, breadcrumbScale, breadcrumbTitleWidth, breadcrumbLocationWidth;
            private Vector2 breadcrumbTipSize;
            private MenuSettings Settings => host.Settings;
            internal float Opacity => windowTransition.Value;
            internal bool Closing => closing;
            internal bool ReadyToClose => closing && SamplePresence() <= 0f;

            internal NativeSurface(MenuWindow host)
            {
                this.host = host;
                drawFrame = DrawFrame;
                search = new UiTextField(ui);
                searchLoader = new UiLoader(ui);
                toast = new UiToast(ui);
                scope = new UiButton(ui); theme = new UiButton(ui);
                help = new UiButton(ui); manage = new UiButton(ui);
                back = new UiButton(ui); contextButton = new UiButton(ui); reset = new UiButton(ui); retry = new UiButton(ui);
                previous = new UiButton(ui); next = new UiButton(ui); done = new UiButton(ui);
                cancelReset = new UiButton(ui); confirmReset = new UiButton(ui); closeHelp = new UiButton(ui);
                navigationToggle = new UiButton(ui);
                drawRailPanel = DrawRailPanel;
                drawContextContents = DrawContextContents;
                closeId = ui.CreateId();
                closeTip = new UiTooltip(ui); themeTip = new UiTooltip(ui); breadcrumbTip = new UiTooltip(ui);
                transition = new UiPageTransition(ui);
                railScroll = new UiScrollView(ui); resultsScroll = new UiScrollView(ui);
                managementScroll = new UiScrollView(ui); contextScroll = new UiScrollView(ui);
                resultsScroll.SetPosition(host.resultScroll.y);
                if (Settings.RememberState)
                {
                    railScroll.SetPosition(RememberedScroll("railScroll"));
                    managementScroll.SetPosition(RememberedScroll("managementScroll"));
                }
                navigationDrawer = new UiDrawer(ui); contextDrawer = new UiDrawer(ui);
                resetDialog = new UiDialog(ui); helpDialog = new UiDialog(ui);
                drawRail = DrawRail; drawResults = DrawResults; drawPage = DrawPage;
                drawContext = DrawContext; drawReset = DrawReset; drawHelp = DrawHelp;
                InitializeManagement();
#if RIMWORLD_1_7
                mainButtonsId = ui.CreateId();
                mainButtonsBackButton = new UiButton(ui);
                drawMainButtons = DrawMainButtons;
#endif
                InitializeSidebarContext();
                for (int i = 0; i < resultButtons.Length; i++) resultButtons[i] = new UiButton(ui);
                SyncCatalog();
            }

            private float SamplePresence() => windowTransition.Sample(Closing, Settings.ReducedMotion);

            internal void PrepareWindow()
            {
                viewport = new Rect(0f, 0f, host.windowRect.width, host.windowRect.height);
                scale = Settings.Scale;
                rowHeight = (Settings.Density == 0 ? 43f : Settings.Density == 2 ? 63f : 53f) * scale;
                narrow = viewport.width < 880f * scale;
                SamplePresence();
            }

            internal void BeginClose() { closing = true; SamplePresence(); }

            internal void ShowToast(string message, UiToastTone tone)
            {
                if (!Settings.ShowToasts) return;
                pendingToast = message;
                pendingTone = tone;
            }


            private void PrepareNotifications()
            {
                if (!Settings.ShowToasts) { pendingToast = null; editPending = false; toast.Dismiss(); return; }
                if (Opacity < 1f) return;
                if (pendingToast != null)
                {
                    toast.DefaultDurationSeconds = Settings.ToastDuration;
                    toast.Show(pendingToast, pendingTone);
                    pendingToast = null;
                }
                else if (editPending && ui.Time - lastEdit >= 3d)
                {
                    editPending = false;
                    toast.DefaultDurationSeconds = Settings.ToastDuration;
                    toast.Show(T("IM_SaveHint"), UiToastTone.Info);
                }
            }

            internal void RecordEdit()
            {
                editPending = true;
                lastEdit = ui.Time;
                toast.Dismiss();
            }

            internal HostedGuiTheme.Scope AdaptHostedCallback() => hostedTheme.Enter(ui, host.session.Selected.Id);


            internal void Open()
            {
                closing = false;
                windowTransition.Open();
                editPending = false;
                lastEdit = Time.realtimeSinceStartup;
                pendingToast = null;
                toast.Dismiss();
                resultsScroll.SetPosition(host.resultScroll.y);
                managementOpen = host.browse.Managing;
            }

            private float RememberedScroll(string key)
            {
                return float.TryParse(host.View(key), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float value) && !float.IsNaN(value) && !float.IsInfinity(value)
                    ? Mathf.Max(0f, value) : 0f;
            }

            internal void CaptureScroll(Dictionary<string, string> view)
            {
                view["railScroll"] = railScroll.Position.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
                view["managementScroll"] = managementScroll.Position.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }

            internal void Navigated(MenuPage page, string anchor)
            {
                SyncCatalog();
#if RIMWORLD_1_7
                mainButtonsOpen = false;
#endif
                managementOpen = navigationOpen = contextOpen = resetOpen = helpOpen = false;
                resetTarget = null;
                pageChanged = true;
                PageState target = GetPage(page);
                if (!string.IsNullOrEmpty(anchor) && target?.Native != null)
                    target.Native.Focus(anchor);
            }

            internal void Draw(Rect rect)
            {
                if (!windowTransition.Draw(viewport, Settings.ReducedMotion, drawFrame)) DrawFrame();
            }

            private void DrawFrame()
            {
                var saved = new GuiState();
                try
                {
                    ui.Scale = scale;
                    ui.BeginFrame(viewport, Settings.Theme, Settings.ReducedMotion || windowTransition.Active);
                    ui.FontSize = 16f * scale;
                    SyncCatalog();
#if RIMWORLD_1_7
                    if (mainButtonsOpen) SyncMainButtonsPage();
#endif
                    pageChanged = false;
                    selected = GetPage(host.session.Selected);
                    if (resetOpen && !ReferenceEquals(resetTarget, selected)) { resetOpen = false; resetTarget = null; }
                    if (!MainButtonsOpen && !managementOpen && !host.browse.ShowResults)
                    {
                        if (selected?.Native != null && !selected.Failed)
                        {
                            try { selected.Native.Prepare(); }
                            catch (ExitGUIException) { throw; }
                            catch (Exception exception) { selected.Failed = true; ReportFailure("prepare native " + selected.Page.Id, exception); }
                        }
                    }
                    navigationDrawer.Prepare(navigationOpen);
                    contextDrawer.Prepare(contextOpen);
                    resetDialog.Prepare(resetOpen);
                    helpDialog.Prepare(helpOpen);
                    PrepareSidebarContext();
                    PrepareNotifications();
                    using (!ui.IsRepaint && (Closing || !Find.WindowStack.GetsInput(host)) ? ui.Input(false) : default(UiContext.UiScope))
                    using (ui.Opacity(1f))
                    using (MenuItemControls.Enter(ui))
                    {
                        backdrop.Draw(ui, viewport, Settings.MarqueeText);
                        if (!ui.IsRepaint) toast.Draw(new Rect(0f, 88f * scale, viewport.width, Mathf.Max(0f, viewport.height - 152f * scale)), scale);
                        using (ui.Input(!toast.BlocksPointer))
                        {
                        HandleSearchKeys();
                        DrawHeader();
                        DrawWorkspace();
                        DrawFooter();
                        if (!MainButtonsOpen && !managementOpen && !pageChanged && !host.browse.ShowResults && selected?.Native != null && !selected.Failed)
                        {
                            try { DrawNativeOverlay(); }
                            catch (ExitGUIException) { throw; }
                            catch (Exception exception) { selected.Failed = true; ReportFailure("overlay native " + selected.Page.Id, exception); }
                        }
                        if (!MainButtonsOpen && !managementOpen && !pageChanged && !host.browse.ShowResults && selected?.Native == null)
                            hostedTheme.DrawHoverCards(viewport);
                        DrawOverlays();
                        HandleKeys();
                        closeTip.DrawOverlay(viewport, new Vector2(180f * scale, 44f * scale), scale);
                        themeTip.DrawOverlay(viewport, new Vector2(220f * scale, 44f * scale), scale);
                        toolbar?.DrawOverlay(viewport, scale);
#if RIMWORLD_1_7
                        if (mainButtonsOpen && !pageChanged) mainButtonsPage.DrawOverlay(viewport);
#endif
                        float tooltipFont = ui.FontSize;
                        ui.FontSize = 12f * scale;
                        breadcrumbTip.DrawOverlay(viewport, breadcrumbTipSize, scale);
                        selected?.Group?.Tabs?.DrawTooltip(viewport, scale);
                        ui.FontSize = tooltipFont;
                        DrawSidebarContext();
                        }
                        if (ui.IsRepaint) toast.Draw(new Rect(0f, 88f * scale, viewport.width, Mathf.Max(0f, viewport.height - 152f * scale)), scale);
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
#if RIMWORLD_1_7
                if (toolbar == null || !ReferenceEquals(toolbarLanguage, host.catalogLanguage))
                {
                    toolbarLanguage = host.catalogLanguage;
                    toolbar = new UiToolbar(ui, new[] { new UiToolbarItem("main-buttons", T("IM_MainButtonsTitle"), UiGlyphKind.Grid) });
                }
                float toolbarWidth = toolbar.MeasureWidth(scale);
#else
                float toolbarWidth = 0f;
#endif
                float width = Mathf.Min(380f * scale, viewport.width * 0.23f);
                width = Mathf.Clamp(width, Mathf.Min(220f * scale, Mathf.Max(80f * scale, themeRect.x - x - 188f * scale - toolbarWidth - 14f * scale)),
                    Mathf.Max(80f * scale, themeRect.x - x - 188f * scale - toolbarWidth - 14f * scale));
                Rect field = new Rect(themeRect.x - width - 14f * scale, close.y, width, close.height);
#if RIMWORLD_1_7
                Rect tools = new Rect(field.x - toolbarWidth - 14f * scale, close.y, toolbarWidth, close.height);
                if (toolbar.Draw(tools, scale, !Closing) == 0) MenuWindow.OpenMainButtons();
#endif
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
#if RIMWORLD_1_7
                if (mainButtonsOpen)
                {
                    DrawMainButtonsArea(main);
                    return;
                }
#endif
                if (managementOpen)
                {
                    DrawManagementPage(main);
                    return;
                }
                if (host.browse.ShowResults)
                {
                    if (SearchQuery.IsModLookup)
                    {
                        DrawManagementPage(main, true);
                        return;
                    }
                    DrawResultArea(main);
                    return;
                }
                if (host.browse.ViewingTarget && back.Draw(new Rect(main.x, main.y, Mathf.Min(200f * scale, main.width), 34f * scale),
                    T("IM_BackResults"), variant: UiButtonVariant.Text, scale: scale) && host.session.Save())
                { host.browse.Back(); }
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
                    ui.Fill(page, ui.Palette.Surface);
                    Rect content = page.ContractedBy(16f * scale);
                    transition.Draw(content, selected.Id, drawPage);
                }
                else
                {
                    ui.Fill(page, WithAlpha(ui.Palette.Sheet, Settings.PanelOpacity));
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
                string title = MainButtonsOpen ? "IrisMenus" : managementOpen ? T("IM_Manage") : host.browse.ShowResults ? T("IM_NativeSearchResults") : host.session.Selected?.DisplayModName ?? T("IM_Global");
#if RIMWORLD_1_7
                string location = mainButtonsOpen ? mainButtonsTitle : managementOpen ? T("IM_NativeLibrary") : host.browse.ShowResults ? host.catalog.Results.Count.ToString() : T("IM_NativeModSettings");
#else
                string location = managementOpen ? T("IM_NativeLibrary") : host.browse.ShowResults ? host.catalog.Results.Count.ToString() : T("IM_NativeModSettings");
#endif
                float breadcrumbWidth = rect.width;
                UiTabs tabs = !MainButtonsOpen && !managementOpen && !host.browse.ShowResults ? selected?.Group?.Tabs : null;
                if (tabs != null)
                {
                    ui.FontSize = 13f * scale;
                    if (tabs.PreparePagination(Mathf.Max(1f, rect.width - 108f * scale), scale)) breadcrumbWidth = Mathf.Max(0f, breadcrumbWidth - 212f * scale);
                }
                if (breadcrumbTitle != title || breadcrumbLocation != location || breadcrumbAvailable != breadcrumbWidth || breadcrumbScale != scale)
                {
                    breadcrumbTitle = title; breadcrumbLocation = location;
                    breadcrumbAvailable = breadcrumbWidth; breadcrumbScale = scale;
                    ui.FontSize = 12f * scale; ui.FontStyle = FontStyle.Normal;
                    breadcrumbLocationWidth = Mathf.Min(UiTextMetrics.Width(ui, location), Mathf.Max(0f, breadcrumbWidth - 48f * scale));
                    float available = Mathf.Max(0f, breadcrumbWidth - breadcrumbLocationWidth - 60f * scale);
                    ui.FontSize = 16f * scale; ui.FontStyle = FontStyle.Bold;
                    breadcrumbTitleWidth = Mathf.Min(UiTextMetrics.Width(ui, title) + 2f * scale, available);
                    breadcrumbDisplay = UiTextMetrics.Ellipsize(ui, title, breadcrumbTitleWidth);
                    ui.FontSize = 12f * scale; ui.FontStyle = FontStyle.Normal;
                    float tipWidth = Mathf.Min(420f * scale, Mathf.Max(1f, viewport.width - 24f * scale));
                    breadcrumbTipSize = new Vector2(tipWidth, Mathf.Min(viewport.height, UiTextMetrics.Height(ui, title, Mathf.Max(1f, tipWidth - 20f * scale)) + 20f * scale));
                }
                ui.FontSize = 16f * scale;
                ui.FontStyle = FontStyle.Bold;
                Rect titleRect = new Rect(rect.x + 22f * scale, rect.y, breadcrumbTitleWidth, 40f * scale);
                using (ui.Clip(titleRect)) ui.Label(new Rect(0f, 0f, titleRect.width, titleRect.height), breadcrumbDisplay, p.Ink);
                breadcrumbTip.Delay = Settings.TipDelay;
                breadcrumbTip.Observe(titleRect, title, enabled: breadcrumbDisplay != title);
                float separatorX = titleRect.xMax + 8f * scale;
                UiGlyph.Draw(ui, new Rect(separatorX, rect.y + 13f * scale, 14f * scale, 14f * scale), UiGlyphKind.ChevronRight, p.Muted);
                ui.FontSize = 12f * scale;
                ui.FontStyle = FontStyle.Normal;
                ui.Label(new Rect(separatorX + 22f * scale, rect.y, breadcrumbLocationWidth, 40f * scale), location, p.Muted);
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
                    using (ui.Clip(content)) DrawNativeContent(content);
                }
                catch (ExitGUIException) { throw; }
                catch (Exception exception) { selected.Failed = true; ReportFailure("draw native " + selected.Page.Id, exception); }
            }

            private void DrawNativeOverlay()
            {
                bool changed = GUI.changed;
                GUI.changed = false;
                try
                {
                    selected.Native.DrawOverlay(viewport);
                    if (GUI.changed) RecordEdit();
                }
                finally { GUI.changed = changed; }
            }

            private static Color WithAlpha(Color color, float alpha) { color.a *= alpha; return color; }

            private void DrawNativeContent(Rect content)
            {
                bool changed = GUI.changed;
                GUI.changed = false;
                try
                {
                    selected.Native.Draw(new Rect(0f, 0f, content.width, content.height));
                    if (GUI.changed) RecordEdit();
                }
                finally { GUI.changed = changed; }
            }

            private void DrawFooter()
            {
                UiPalette p = ui.Palette;
                float h = 40f * scale;
                Rect footer = new Rect(0f, viewport.height - h, viewport.width, h);
                ui.Fill(footer, p.Footer);
                ui.Fill(new Rect(footer.x, footer.y, footer.width, 1f), p.FooterLine);
                float oldSize = ui.FontSize;
                ui.FontSize = 11f * scale;
                float helpX = viewport.width - 68f * scale;
                if (viewport.width > 900f * scale)
                {
                    Rect key = new Rect(helpX - 112f * scale, footer.y + 9f * scale, 35f * scale, 22f * scale);
                    ui.Border(key, p.FooterLine);
                    ui.Label(key, "Esc", p.FooterMuted, TextAnchor.MiddleCenter);
                    ui.Label(new Rect(key.xMax + 8f * scale, footer.y, 60f * scale, h), T("Back"), p.FooterMuted);
                }
                float metadataRight = viewport.width > 900f * scale ? helpX - 140f * scale : helpX - 24f * scale;
                DrawFooterMetadata(new Rect(36f * scale, footer.y, Mathf.Max(0f, metadataRight - 36f * scale), h));
                Rect helpRect = new Rect(helpX, footer.y + 4f * scale, 32f * scale, h - 8f * scale);
                UiInteraction helpHit = ui.Interact(help.Id, helpRect);
                float helpHover = footerHelpHover.Step(helpHit.Hovered || helpHit.Pressed ? 1f : 0f, ui.Time, UiMotion.Fast, ui.ReducedMotion);
                ui.Fill(helpRect, WithAlpha(p.FooterLine, helpHover));
                UiGlyph.Draw(ui, helpRect.ContractedBy(8f * scale), UiGlyphKind.Help, p.FooterInk);
                if (helpHit.Focused && ui.FocusVisible) ui.Border(helpRect, p.FooterInk);
                if (helpHit.Clicked) helpOpen = true;
                ui.FontSize = oldSize;
            }

            private void DrawFooterMetadata(Rect rect)
            {
                ModContentPack content = managementOpen || (host.browse.ShowResults && host.browse.Global) ? null : selected?.Page.Content;
                if (content != footerOwner || !ReferenceEquals(footerLanguage, host.catalogLanguage))
                {
                    footerOwner = content;
                    footerLanguage = host.catalogLanguage;
                    if (content != null && !metadata.TryGetValue(content, out NativeModMetadata value))
                    { value = new NativeModMetadata(content); metadata.Add(content, value); }
                    NativeModMetadata item = content == null ? null : metadata[content];
                    footerName = item?.Name ?? string.Empty;
                    footerAuthor = string.IsNullOrWhiteSpace(item?.Author) ? string.Empty : "IM_ModAuthor".Translate(item.Author).ToString();
                    footerVersion = item?.Version ?? string.Empty;
                    footerAvailable = -1f;
                }
                if (footerAvailable != rect.width || footerFont != ui.FontSize)
                {
                    footerAvailable = rect.width; footerFont = ui.FontSize;
                    string version = string.IsNullOrEmpty(footerVersion) ? string.Empty : "  ·  " + footerVersion;
                    float remaining = Mathf.Max(0f, rect.width - UiTextMetrics.Width(ui, version));
                    string name = UiTextMetrics.Ellipsize(ui, footerName, remaining * (footerAuthor.Length > 0 ? 0.48f : 1f));
                    string author = UiTextMetrics.Ellipsize(ui, footerAuthor, Mathf.Max(0f, remaining - UiTextMetrics.Width(ui, name) - 16f * scale));
                    footerDisplay = UiTextMetrics.Ellipsize(ui, name + (author.Length > 0 ? "  " + author : string.Empty) + version, rect.width);
                }
                ui.Label(rect, footerDisplay, ui.Palette.FooterMuted);
            }

            private void HandleSearchKeys()
            {
                if (!ui.IsInputAllowed || Closing) return;
                if (MainButtonsOpen) return;
                Event current = ui.Event;
                if (current != null && current.type == EventType.KeyDown && (current.control || current.command) && current.keyCode == KeyCode.K)
                { ui.Focus(search.Id); current.Use(); }
                if (ui.IsFocused(search.Id) && ui.ConsumeKey(KeyCode.Escape))
                {
                    if (host.browse.Query.Length > 0) ChangeQuery("");
                    ui.Focus(default);
                    GUI.FocusControl(null);
                }
            }

            private void HandleKeys()
            {
                if (!ui.IsInputAllowed || Closing) return;
#if RIMWORLD_1_7
                if (mainButtonsOpen && ui.ConsumeKey(KeyCode.Escape)) { CloseMainButtons(); return; }
#endif
                if (managementOpen && ui.ConsumeKey(KeyCode.Escape)) { CloseManagement(); return; }
                if (!ui.ConsumeKey(KeyCode.Escape)) return;
                if (host.browse.Query.Length > 0) { ChangeQuery(""); ui.Focus(default(UiId)); GUI.FocusControl(null); }
                else if (navigationOpen) navigationOpen = false;
                else host.Close();
            }

#if RIMWORLD_1_7
            internal void OpenMainButtons()
            {
                if (!mainButtonsOpen) mainButtonsPage = null;
                SyncMainButtonsPage();
                mainButtonsOpen = true;
                navigationOpen = contextOpen = resetOpen = helpOpen = false;
                resetTarget = null;
                pageChanged = true;
                ui.Focus(mainButtonsBackButton.Id);
                GUI.FocusControl(null);
            }

            private void SyncMainButtonsPage()
            {
                if (mainButtonsPage != null && ReferenceEquals(mainButtonsLanguage, host.catalogLanguage)) return;
                mainButtonsLanguage = host.catalogLanguage;
                mainButtonsPage = new MainButtonsPage(ui);
                mainButtonsTitle = T("IM_MainButtonsTitle");
                mainButtonsBack = T("Back");
            }

            private void CloseMainButtons()
            {
                mainButtonsOpen = false;
                pageChanged = true;
                ui.Focus(default);
                GUI.FocusControl(null);
            }

            private void DrawMainButtonsArea(Rect rect)
            {
                if (mainButtonsBackButton.Draw(new Rect(rect.x, rect.y, Mathf.Min(rect.width, 160f * scale), 36f * scale),
                    mainButtonsBack, variant: UiButtonVariant.Text, scale: scale))
                {
                    CloseMainButtons();
                    return;
                }
                rect.yMin += 48f * scale;
                transition.Draw(rect, mainButtonsId, drawMainButtons);
            }

            private void DrawMainButtons(Rect rect) => mainButtonsPage.Draw(rect, scale, rowHeight);
#endif

            public void Dispose()
            {
                foreach (PageState page in states.Values) page.Dispose();
                states.Clear();
                DisposeModIcons();
                DisposeManagement();
                windowTransition.Dispose();
                search.Dispose();
                hostedTheme.Dispose();
                backdrop.Dispose();
                ui.Dispose();
            }
        }
    }
}
