using System;
using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed partial class MenuWindow : Window
    {
        private List<MenuPage> adapters;
        private List<MenuPage> pages;
        private MenuCatalog catalog;
        private readonly MenuBrowseState browse = new MenuBrowseState();
        private int catalogRevision = -1;
        private int catalogFrame = -1;
        private object catalogLanguage;
        private Vector2 resultScroll;
        private readonly MenuSession session = new MenuSession(ReportFailure);
        private bool closeSaved;
        private bool restoreResultScroll;
        private MenuPage requestedPage;
        private NativeSurface nativeSurface;
        private readonly Action closeHostedPage;
        internal float Opacity => nativeSurface?.Opacity ?? 0f;
        protected override float Margin => 0f;
        private MenuSettings Settings => IrisMenusMod.Instance.Settings;

        internal bool ShowToast(string message, UI.UiToastTone tone)
        {
            if (nativeSurface == null || nativeSurface.Closing || string.IsNullOrWhiteSpace(message)) return false;
            nativeSurface.ShowToast(message, tone);
            return true;
        }

        public MenuWindow() : this(null) { }

        internal MenuWindow(MenuPage requested) : base(new FullscreenWindowDrawing())
        {
            drawShadow = false;
            forcePause = true;
            absorbInputAroundWindow = true;
            draggable = false;
            resizeable = false;
            doCloseX = false;
            closeOnAccept = false;
            closeOnClickedOutside = false;
            requestedPage = requested;
            closeHostedPage = CloseHostedPage;
            MenuRegistry.AdoptActionOwners();
            MenuOwnerActions.EnsureLaunchers();
            NativeSettingsPages.EnsureRegistered();
            adapters = MenuRegistry.Adaptable();
            pages = MenuRegistry.Registered.Concat(Hosted()).ToList();
            RestoreOpeningState();
            RefreshCatalog();
            nativeSurface = new NativeSurface(this);
        }

        public override Vector2 InitialSize => new Vector2(Verse.UI.screenWidth, Verse.UI.screenHeight);

        public override void PostOpen()
        {
            base.PostOpen();
            ClampGeometry();
            nativeSurface.Open();
        }

        public override void Notify_ResolutionChanged() => ClampGeometry();
        private void ClampGeometry() => windowRect = new Rect(0f, 0f, Verse.UI.screenWidth, Verse.UI.screenHeight);

        public override void WindowOnGUI()
        {
            ClampGeometry();
            nativeSurface.PrepareWindow();
            base.WindowOnGUI();
        }

        public override void WindowUpdate()
        {
            base.WindowUpdate();
            if (nativeSurface.ReadyToClose) base.Close();
        }

        public override void OnCancelKeyPressed() { }

        public override bool OnCloseRequest()
        {
            if (nativeSurface.ReadyToClose) return true;
            if (nativeSurface.Closing) return false;
            if (!session.Save() || !SaveWindowSettings()) return false;
            closeSaved = true;
            nativeSurface.BeginClose();
            return false;
        }

        public override void PreClose()
        {
            try
            {
                // WindowStack can remove a window directly (e.g. return to main menu).
                if (!closeSaved) { session.Save(); SaveWindowSettings(); }
            }
            finally
            {
                session.Deactivate();
                nativeSurface?.Dispose();
                nativeSurface = null;
                catalog?.Dispose();
                base.PreClose();
            }
        }

        internal bool NavigateUri(MenuPage page, Action focus, string anchor = null)
        {
            if (page == null || nativeSurface.Closing || !session.Select(page)) return false;
            browse.Navigate(page);
            RememberPage(page);
            if (session.Selected != page) return false;
            try
            {
                focus?.Invoke();
                nativeSurface.Navigated(page, anchor);
                return true;
            }
            catch (Exception exception) { ReportFailure("open link " + page.Id, exception); return false; }
        }

        private bool SaveWindowSettings()
        {
            if (Settings.RememberState)
            {
                Settings.LastPage = session.Selected?.Id;
                CaptureHostView();
            }
            try { IrisMenusMod.Instance.WriteSettings(); return true; }
            catch (Exception exception) { ReportFailure("save window settings", exception); return false; }
        }

        private void RestoreOpeningState()
        {
            if (!Settings.RememberState)
            {
                MenuRegistry.ResetListingScroll();
                session.Select(requestedPage);
                if (requestedPage != null) browse.Navigate(requestedPage);
                return;
            }
            MenuPage remembered = pages.FirstOrDefault(page => page.Id == Settings.LastPage);
            bool explicitRequest = requestedPage != null;
            MenuPage initial = explicitRequest
                ? pages.FirstOrDefault(page => page.Id == requestedPage.Id) ?? requestedPage
                : remembered ?? AvailableRememberedPage(Settings.LastPage);
            if (initial != null) session.Select(initial);
            Dictionary<string, string> view = Settings.HostView;
            if (!explicitRequest && view.Count > 0)
            {
                view.TryGetValue("scope", out string scope);
                bool scopeAvailable = scope == null || pages.Any(page => page.OwnerId == scope);
                MenuPage target = view.TryGetValue("target", out string targetId)
                    ? pages.FirstOrDefault(page => page.Id == targetId) : null;
                if (target == null && view.TryGetValue("mode", out string mode) && mode == "target")
                    target = initial;
                if (target != null && session.Select(target)) initial = target;
                browse.Restore(view, target ?? initial, scopeAvailable);
                if (browse.ViewingTarget && session.Selected == null)
                {
                    browse.Navigate(null);
                    browse.Search(view.TryGetValue("query", out string query) ? query : "");
                }
            }
            else if (initial != null) browse.Navigate(initial);
            RestoreListingScroll();
            if (explicitRequest) return;
            if (float.TryParse(View("resultScroll"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float result))
            { resultScroll.y = result; restoreResultScroll = result > 0f; }
        }

        private MenuPage AvailableRememberedPage(string lastPage)
        {
            if (lastPage == null) return null;
            foreach (MenuPage page in pages)
                if (lastPage.StartsWith(page.OwnerId + ":", StringComparison.Ordinal)) return page;
            return null;
        }

        private string View(string key) => Settings.HostView.TryGetValue(key, out string value) ? value : null;

        private void CaptureHostView()
        {
            Dictionary<string, string> view = Settings.HostView;
            var listing = new List<string>();
            foreach (string key in view.Keys)
                if (key.StartsWith("listing:", StringComparison.Ordinal)) listing.Add(key);
            foreach (string key in listing) view.Remove(key);
            browse.Capture(view, session.Selected);
            view["resultScroll"] = resultScroll.y.ToString(System.Globalization.CultureInfo.InvariantCulture);
            nativeSurface.CaptureScroll(view);
            foreach (MenuPage page in pages)
            {
                if (page.ListingScroll == null || page.ListingScroll.PositionY <= 0f) continue;
                view["listing:" + page.Id] = page.ListingScroll.PositionY.ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        private void RestoreListingScroll()
        {
            foreach (MenuPage page in pages)
            {
                if (page.ListingScroll == null) continue;
                if (Settings.HostView.TryGetValue("listing:" + page.Id, out string stored) &&
                    float.TryParse(stored, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float offset))
                    page.ListingScroll.PositionY = offset;
            }
        }

        public override void DoWindowContents(Rect inRect)
        {
            ClampGeometry();
            if (catalogRevision != MenuRegistry.Revision || !ReferenceEquals(catalogLanguage, LanguageDatabase.activeLanguage)) RefreshCatalog();
            catalog.Query(browse.Query, browse.Scope);
            if (catalogFrame != Time.frameCount) { catalog.Advance(); catalogFrame = Time.frameCount; }
            nativeSurface.Draw(inRect);
        }


        private IEnumerable<MenuPage> Hosted() => adapters.Where(page => !MenuRegistry.HasPages(page.Owner) && Settings.HostsAdapter(page.Id));


        private void DrawSelected(Rect rect)
        {
            float strip = nativeSurface.DrawNativeActions(rect);
            Rect page = new Rect(rect.x, rect.y + strip, rect.width, Mathf.Max(1f, rect.height - strip));
            if (session.DrawFailed)
            {
                Widgets.Label(new Rect(page.x, page.y, page.width, 56f), "IM_PageFailed".Translate());
                if (Widgets.ButtonText(new Rect(page.x, page.y + 64f, Mathf.Min(160f, page.width), 30f), "IM_Retry".Translate())) session.Retry();
                return;
            }
            var state = new GuiState();
            GUI.BeginGroup(page);
            try
            {
                GUI.changed = false;
                session.Selected?.BindHostClose?.Invoke(closeHostedPage);
                using (nativeSurface.AdaptHostedCallback())
                    session.Draw(new Rect(0f, 0f, page.width, page.height));
                if (GUI.changed) nativeSurface.RecordEdit();
            }
            finally { try { GUI.EndGroup(); } finally { state.Restore(); } }
        }

        private void CloseHostedPage() => Select(null);


        private static void Activate(MenuOwnerAction action)
        {
            if (!action.IsVisible() || !action.IsEnabled())
            {
                Messages.Message("IM_ActionUnavailable".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            string confirmation;
            try { if (!action.TryConfirmText(out confirmation)) return; }
            catch (Exception exception)
            {
                Log.Error("[IrisMenus] Owner action confirmation failed for " + action.OwnerKey + ":" + action.Id + ": " + exception);
                Messages.Message("IM_ActionFailed".Translate(), MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!string.IsNullOrWhiteSpace(confirmation))
            {
                Find.WindowStack.Add(Dialog_MessageBox.CreateConfirmation(confirmation, () => Run(action)));
                return;
            }
            Run(action);
        }

        private static void Run(MenuOwnerAction action)
        {
            if (!action.IsVisible() || !action.IsEnabled()) return;
            try { action.Execute(); }
            catch (Exception exception)
            {
                Log.Error("[IrisMenus] Failed to run " + action.OwnerKey + ":" + action.Id + ": " + exception);
                Messages.Message("IM_ActionFailed".Translate(), MessageTypeDefOf.RejectInput, false);
            }
        }


        private void Select(MenuPage page)
        {
            if (!session.Select(page)) return;
            browse.Navigate(page);
            resultScroll = Vector2.zero;
            RememberPage(page);
            GUI.FocusControl(null);
        }

        internal bool OpenPage(MenuPage page)
        {
            MenuPage owned = MenuRegistry.Registered.Concat(adapters).FirstOrDefault(candidate => candidate.Id == page.Id) ?? page;
            if (!session.Select(owned)) return false;
            browse.Navigate(owned);
            resultScroll = Vector2.zero;
            RememberPage(owned);
            nativeSurface.Navigated(owned, null);
            return true;
        }

        private void RememberPage(MenuPage page)
        {
            if (page != null && Settings.RememberState) Settings.LastSubItems[page.OwnerId] = page.Id;
        }


        private void RefreshCatalog()
        {
            catalog?.Dispose();
            MenuRegistry.AdoptActionOwners();
            adapters = MenuRegistry.Adaptable();
            pages = MenuRegistry.Registered.Concat(Hosted()).ToList();
            RebindSelection();
            catalog = new MenuCatalog(pages);
            catalog.Query(browse.Query, browse.Scope);
            catalogRevision = MenuRegistry.Revision;
            catalogLanguage = LanguageDatabase.activeLanguage;
            catalogFrame = -1;
        }

        private void RebindSelection()
        {
            MenuPage selected = session.Selected;
            MenuPage current = MenuSession.ResolveReplacement(selected, pages);
            if (ReferenceEquals(current, selected)) return;
            session.Rebind(current);
            if (browse.Managing) return;
            if (current == null)
            {
                browse.Navigate(null);
                return;
            }
            if (selected != null && selected.Id == current.Id) return;
            if (browse.ViewingTarget || browse.ShowResults) return;
            browse.Navigate(current);
        }


        private static void ReportFailure(string operation, Exception exception)
        {
            Log.Error("[IrisMenus] Failed to " + operation + ": " + exception);
            Messages.Message("IM_OperationFailed".Translate(), MessageTypeDefOf.RejectInput, false);
        }
    }
}
