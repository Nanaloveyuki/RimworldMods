using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed class IrisMenusMod : Mod
    {
        internal static IrisMenusMod Instance;
        internal readonly MenuSettings Settings;

        public IrisMenusMod(ModContentPack content) : base(content)
        {
            Instance = this;
            Settings = GetSettings<MenuSettings>();
            SettingsEntryRouter.Install();
#if RIMWORLD_1_7
            MainButtonVisibility.Install();
#endif
        }

        public override string SettingsCategory() => "IrisMenus";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y, Mathf.Min(inRect.width, 240f), 32f), "IM_Open".Translate()))
                MenuRegistry.Open();
            bool remember = Settings.RememberState;
            Rect rememberRect = new Rect(inRect.x, inRect.y + 44f, Mathf.Min(inRect.width, 320f), 28f);
            Widgets.CheckboxLabeled(rememberRect, "IM_RememberState".Translate(), ref remember);
            TooltipHandler.TipRegion(rememberRect, "IM_RememberStateTip".Translate());
            bool changed = remember != Settings.RememberState;
            if (changed)
            {
                Settings.RememberState = remember;
                if (!remember) Settings.ClearRememberedView();
            }
            bool showButton = Settings.ShowMainButton;
            Rect buttonRect = new Rect(inRect.x, inRect.y + 80f, Mathf.Min(inRect.width, 420f), 28f);
            Widgets.CheckboxLabeled(buttonRect, "IM_ShowMainButton".Translate(), ref showButton);
            TooltipHandler.TipRegion(buttonRect, "IM_ShowMainButtonTip".Translate());
            if (showButton != Settings.ShowMainButton)
            {
                Settings.ShowMainButton = showButton;
                changed = true;
            }
            if (changed) WriteSettings();
#if RIMWORLD_1_7
            if (Widgets.ButtonText(new Rect(inRect.x, inRect.y + 124f, Mathf.Min(inRect.width, 320f), 32f), "IM_MainButtonsTitle".Translate()))
                MenuWindow.OpenMainButtons();
#endif
        }
    }

    public sealed class MenuSettings : ModSettings
    {
        internal List<string> EnabledAdapters = new List<string>();
        internal List<string> SidebarPins = new List<string>();
        internal List<string> SidebarOrder = new List<string>();
        internal Dictionary<string, string> SidebarAliases = new Dictionary<string, string>();
        internal bool RememberState = true;
        internal bool ShowMainButton = true;
        internal List<string> HiddenMainButtons = new List<string>();
        internal IrisMenus.UI.UiTheme Theme = IrisMenus.UI.UiTheme.Light;
        internal bool ReducedMotion;
        internal float Scale = 1f;
        internal int Density = 1;
        internal float PanelOpacity = 0.94f;
        internal bool ShowTips = true;
        internal float TipDelay = 0.3f;
        internal bool SearchCurrentMod;
        internal string MarqueeText = "IRIS MENUS";
        internal float ToastDuration = 3f;
        internal bool ShowToasts = true;
        internal List<ItemValueBand> ItemValueBands = ItemValueColors.CreateDefaults();
        internal string LastPage;
        internal Dictionary<string, string> LastSubItems = new Dictionary<string, string>();
        internal Dictionary<string, string> HostView = new Dictionary<string, string>();

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref EnabledAdapters, "enabledAdapters", LookMode.Value);
            Scribe_Values.Look(ref LastPage, "lastPage");
            Scribe_Values.Look(ref RememberState, "rememberState", true);
            Scribe_Values.Look(ref ShowMainButton, "showMainButton", true);
            Scribe_Collections.Look(ref HiddenMainButtons, "hiddenMainButtons", LookMode.Value);
            if (HiddenMainButtons == null) HiddenMainButtons = new List<string>();
            LookMigrated(ref Theme, "theme", "nativeTheme", IrisMenus.UI.UiTheme.Light);
            Scribe_Values.Look(ref ReducedMotion, "reducedMotion", false);
            LookMigrated(ref Scale, "scale", "nativeScale", 1f);
            LookMigrated(ref Density, "density", "nativeDensity", 1);
            LookMigrated(ref PanelOpacity, "panelOpacity", "nativePanelOpacity", 0.94f);
            Scribe_Values.Look(ref ShowTips, "showTips", true);
            Scribe_Values.Look(ref TipDelay, "tipDelay", 0.3f);
            Scribe_Values.Look(ref SearchCurrentMod, "searchCurrentMod", false);
            Scribe_Values.Look(ref MarqueeText, "marqueeText", "IRIS MENUS");
            Scribe_Values.Look(ref ToastDuration, "toastDuration", 3f);
            Scribe_Values.Look(ref ShowToasts, "showToasts", true);
            Scribe_Collections.Look(ref ItemValueBands, "itemValueBands", LookMode.Deep);
            Scribe_Collections.Look(ref LastSubItems, "lastSubItems", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref HostView, "hostView", LookMode.Value, LookMode.Value);
            Scribe_Collections.Look(ref SidebarPins, "sidebarPins", LookMode.Value);
            Scribe_Collections.Look(ref SidebarOrder, "sidebarOrder", LookMode.Value);
            Scribe_Collections.Look(ref SidebarAliases, "sidebarAliases", LookMode.Value, LookMode.Value);
            if (SidebarPins == null) SidebarPins = new List<string>();
            if (SidebarOrder == null) SidebarOrder = new List<string>();
            if (SidebarAliases == null) SidebarAliases = new Dictionary<string, string>();
            if (EnabledAdapters == null) EnabledAdapters = new List<string>();
            if (LastSubItems == null) LastSubItems = new Dictionary<string, string>();
            if (HostView == null) HostView = new Dictionary<string, string>();
            if (Scribe.mode == LoadSaveMode.LoadingVars)
            {
                MigrateHostScroll("navigationScroll", "railScroll");
                MigrateHostScroll("adapterScroll", "managementScroll");
                HostView.Remove("showNavigation");
            }
            if (Scribe.mode == LoadSaveMode.PostLoadInit) ItemValueColors.Normalize(ref ItemValueBands);
            Scale = Mathf.Clamp(Finite(Scale, 1f), 0.8f, 1.3f);
            Density = System.Math.Max(0, System.Math.Min(2, Density));
            PanelOpacity = Mathf.Clamp01(Finite(PanelOpacity, 0.94f));
            TipDelay = Mathf.Clamp(Finite(TipDelay, 0.3f), 0.1f, 0.8f);
            MarqueeText = MarqueeText ?? "IRIS MENUS";
            ToastDuration = Mathf.Clamp(Finite(ToastDuration, 3f), 1f, 10f);
        }

        private static void LookMigrated<T>(ref T value, string key, string legacyKey, T defaultValue)
        {
            T fallback = defaultValue;
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref fallback, legacyKey, defaultValue);
            Scribe_Values.Look(ref value, key, fallback);
        }

        private void MigrateHostScroll(string legacyKey, string key)
        {
            if (!HostView.ContainsKey(key) && HostView.TryGetValue(legacyKey, out string value))
                HostView[key] = value;
            HostView.Remove(legacyKey);
        }

        internal static float Finite(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;

        internal void ClearRememberedView()
        {
            LastPage = null;
            LastSubItems.Clear();
            HostView.Clear();
            MenuRegistry.ResetListingScroll();
        }
    }

    public sealed class MainButtonWorker_IrisMenus : MainButtonWorker
    {
        public override bool Visible => base.Visible && (IrisMenusMod.Instance?.Settings.ShowMainButton ?? true);
        public override void Activate() => MenuRegistry.Open();
    }
}
