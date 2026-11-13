extern alias iris;
using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using iris::IrisMenus;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal sealed partial class RHAH_IrisMenusPages
    {
        void DrawDiagnosticPawns(Listing_Standard list)
        {
            DrawPawns(list);
        }

        void DrawDiagnosticEvents(Listing_Standard list)
        {
            if (!Prefs.DevMode)
                RHAH_IrisMenusWidgets.Quote(list, "dev-events-note", "RHAH_Menu_DevEvents_Note".Translate());
            DrawDevEvents(list);
        }

        IEnumerable<MenuSearchEntry> SearchDiagnosticPawns()
        {
            foreach (MenuSearchEntry entry in SearchPawns()) yield return entry;
        }

        IEnumerable<MenuSearchEntry> SearchDiagnosticEvents()
        {
            foreach (MenuSearchEntry entry in SearchDevEvents()) yield return entry;
        }

        void DrawExperimental(Listing_Standard list)
        {
            Section(list, "RHAH_Menu_Experimental");
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (settings == null)
            {
                Empty(list, "RHAH_Menu_Settings_Missing");
                return;
            }
            MenuControls.Anchor(list, "optimize-generation");
            RHAH_IrisMenusWidgets.Checkbox(list, "setting-" + nameof(settings.optimizeGeneration),
                "RHAH_Settings_OptimizeGeneration".Translate(), ref settings.optimizeGeneration,
                "RHAH_Settings_OptimizeGeneration_Tooltip".Translate());
            MenuControls.Anchor(list, "stagger-generation");
            RHAH_IrisMenusWidgets.Checkbox(list, "setting-" + nameof(settings.staggerGeneration),
                "RHAH_Settings_Stagger".Translate(), ref settings.staggerGeneration,
                "RHAH_Settings_Stagger_Tooltip".Translate());
        }

        static IEnumerable<MenuSearchEntry> SearchArrivalOptions()
        {
            yield return Entry("setting-aidRequestsEnabled", "RHAH_Settings_AidRequests");
            yield return Entry("setting-intelTradesEnabled", "RHAH_Settings_IntelTrades");
            yield return Entry("setting-visitorChoicesEnabled", "RHAH_Settings_VisitorChoices");
            yield return Entry("setting-foodGiveHintDismissed", "RHAH_Settings_FoodGiveHint");
            yield return Entry("setting-traderIgnoresHarshEnvironment", "RHAH_Settings_TraderIgnoreEnvironment");
            yield return Entry("setting-traderIgnoresEnclosedSpace", "RHAH_Settings_TraderIgnoreEnclosed");
            yield return Entry("setting-spaceApproachEnabled", "RHAH_Settings_SpaceApproach");
            if (RHAH_SkylandCompat.Active)
                yield return Entry("setting-skylandIncidentsEnabled", "RHAH_Settings_SkylandIncidents");
            yield return Entry("approach-speed", "RHAH_Settings_ApproachSpeed", "caravan speed travelers");
            yield return Entry("setting-childExchangeFoodSubstitution", "RHAH_Settings_ChildExchangeFood");
        }

        static IEnumerable<MenuSearchEntry> SearchReliefRules()
        {
            yield return Entry("relief-enabled", "RHAH_Settings_ReliefEnabled");
            yield return Entry("relief-outside", "RHAH_Settings_EatOutsideRelief");
            yield return Entry("relief-ignore", "RHAH_Settings_IgnoreReliefAfterFed");
            yield return Entry("relief-leave", "RHAH_Settings_LeaveAfterFed");
        }

        IEnumerable<MenuSearchEntry> SearchReliefFoods() =>
            SearchFoods(RHAH_IrisMenusItemKind.Relief, "relief-foods", "relief-food-", "RHAH_Settings_ReliefFoods");

        IEnumerable<MenuSearchEntry> SearchGiveFoods() =>
            SearchFoods(RHAH_IrisMenusItemKind.Give, "give-foods", "give-food-", "RHAH_Settings_GiveFoods");

        IEnumerable<MenuSearchEntry> SearchBegFoods() =>
            SearchFoods(RHAH_IrisMenusItemKind.Beg, "beg-foods", "beg-food-", "RHAH_Settings_BegFoods");

        IEnumerable<MenuSearchEntry> SearchFoods(RHAH_IrisMenusItemKind kind, string root, string prefix, string titleKey)
        {
            yield return Entry(root, titleKey);
            yield return Entry(prefix + "search", titleKey + "_Search");
            if (kind != RHAH_IrisMenusItemKind.Relief)
                yield return Entry(prefix + "sort", titleKey + "_Sort");
            var foods = new List<ThingDef>();
            if (kind == RHAH_IrisMenusItemKind.Beg) RHAH_ReliefFood.AppendBegFoods(foods);
            else RHAH_ReliefFood.AppendCandidateFoods(foods);
            RHAH_Settings settings = RHAH_Mod.Settings;
            int mode = kind == RHAH_IrisMenusItemKind.Give ? settings?.giveFoodListMode ?? 0 :
                kind == RHAH_IrisMenusItemKind.Beg ? settings?.begFoodListMode ?? 0 : 0;
            for (int i = 0; i < foods.Count; i++)
            {
                ThingDef food = foods[i];
                if (food == null || string.IsNullOrEmpty(food.defName)) continue;
                string key = RHAH_ReliefFood.GroupKey(mode, food);
                string label = food.LabelCap;
                yield return new MenuSearchEntry(prefix + food.defName, () => label,
                    () => food.defName + " " + key, () => GiveFoodTitle(mode, key));
            }
        }

        void FocusFood(RHAH_IrisMenusItemKind kind, string prefix, string anchor)
        {
            if (anchor == null || !anchor.StartsWith(prefix, StringComparison.Ordinal)) return;
            ThingDef food = DefDatabase<ThingDef>.GetNamedSilentFail(anchor.Substring(prefix.Length));
            if (food == null) return;
            RHAH_Settings settings = RHAH_Mod.Settings;
            int mode = kind == RHAH_IrisMenusItemKind.Give ? settings?.giveFoodListMode ?? 0 :
                kind == RHAH_IrisMenusItemKind.Beg ? settings?.begFoodListMode ?? 0 : 0;
            string key = RHAH_ReliefFood.GroupKey(mode, food);
            if (kind == RHAH_IrisMenusItemKind.Relief)
            {
                foodQuery = string.Empty;
                pendingFoodMod = key;
            }
            else if (kind == RHAH_IrisMenusItemKind.Give)
            {
                giveFoodQuery = string.Empty;
                pendingGiveFoodGroup = key;
            }
            else
            {
                begFoodQuery = string.Empty;
                pendingBegFoodGroup = key;
            }
        }
    }
}
