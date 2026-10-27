#if RHAH_IRISMENUS
extern alias iris;
using System;
using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.Generation;
using HungerAndHavoc.Incidents;
using iris::IrisMenus;
using iris::IrisMenus.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal sealed partial class RHAH_IrisMenusPages
    {
        readonly RHAH_IrisMenusItemList reliefItems = new RHAH_IrisMenusItemList(RHAH_IrisMenusItemKind.Relief);
        readonly RHAH_IrisMenusItemList giveItems = new RHAH_IrisMenusItemList(RHAH_IrisMenusItemKind.Give);
        readonly RHAH_IrisMenusItemList begItems = new RHAH_IrisMenusItemList(RHAH_IrisMenusItemKind.Beg);
        readonly RHAH_IrisMenusItemList demandItems = new RHAH_IrisMenusItemList(RHAH_IrisMenusItemKind.Demand);
        readonly RHAH_IrisMenusItemList apparelItems = new RHAH_IrisMenusItemList(RHAH_IrisMenusItemKind.Apparel);
        readonly Dictionary<string, RHAH_IrisMenusChoiceLabels> sortChoices = new Dictionary<string, RHAH_IrisMenusChoiceLabels>();
        readonly Dictionary<string, UiThingEntry> temperatureItems = new Dictionary<string, UiThingEntry>();
        readonly MenuThingsList temperatureList = new MenuThingsList("temp-", "temp-group-");
        object temperatureLanguage;

        static bool DrawContentFold(Listing_Standard list, string id, string title, bool open)
        {
            Rect row = list.GetRect(28f);
            if (RHAH_IrisMenusWidgets.Native != null)
                return RHAH_IrisMenusWidgets.Button(row, "fold-" + id, (open ? "−  " : "+  ") + title);
            if (!RHAH_IrisMenusWidgets.IsVisible(row)) return false;
            Widgets.DrawHighlightIfMouseover(row);
            Widgets.DrawTextureFitted(new Rect(row.x, row.y + 2f, 24f, 24f), open ? TexButton.Collapse : TexButton.Reveal, 0.7f);
            Widgets.Label(new Rect(row.x + 26f, row.y, row.width - 26f, row.height), title);
            return Widgets.ButtonInvisible(row);
        }

        bool DrawTemperatureItem(Listing_Standard list, ThingDef def, string defName, string label, bool enabled)
        {
            if (def == null)
            {
                RHAH_IrisMenusWidgets.Checkbox(list, "temp-" + defName, "RHAH_Settings_TemperatureApparel".Translate(label), ref enabled, "RHAH_Settings_TemperatureApparel_Tooltip".Translate());
                return enabled;
            }
            if (!ReferenceEquals(temperatureLanguage, LanguageDatabase.activeLanguage))
            {
                temperatureItems.Clear();
                temperatureLanguage = LanguageDatabase.activeLanguage;
            }
            if (!temperatureItems.TryGetValue(defName, out UiThingEntry entry))
            {
                entry = new UiThingEntry(def, defName, "RHAH_Settings_TemperatureApparel".Translate(label), tooltip: "RHAH_Settings_TemperatureApparel_Tooltip".Translate());
                temperatureItems.Add(defName, entry);
            }
            return temperatureList.DrawItem(list, entry, enabled);
        }
    }

    internal sealed class RHAH_IrisMenusChoiceLabels
    {
        readonly string key;
        readonly int count;
        object language;
        string[] options;
        internal RHAH_IrisMenusChoiceLabels(string key, int count) { this.key = key; this.count = count; }
        internal string[] Options
        {
            get
            {
                if (options == null || !ReferenceEquals(language, LanguageDatabase.activeLanguage))
                {
                    language = LanguageDatabase.activeLanguage;
                    options = new string[count];
                    for (int i = 0; i < count; i++) options[i] = (key + "_" + i).Translate();
                }
                return options;
            }
        }
    }

    internal enum RHAH_IrisMenusItemKind { Relief, Give, Beg, Demand, Apparel }

    internal sealed class RHAH_IrisMenusItemList
    {
        readonly RHAH_IrisMenusItemKind kind;
        readonly string rootAnchor, itemPrefix, translation, sortAnchor, searchAnchor, searchControl, noteAnchor, allAnchor, noneAnchor, resetAnchor;
        readonly MenuThingsList view;
        readonly Func<UiThingEntry, bool> selected;
        readonly Action<UiThingEntry, bool> assign;
        readonly Action<int> assignMode;
        readonly List<ThingDef> candidates = new List<ThingDef>();
        readonly List<string> names = new List<string>();
        readonly Dictionary<ThingDef, UiThingEntry> entries = new Dictionary<ThingDef, UiThingEntry>();
        readonly RHAH_IrisMenusChoiceLabels choices;
        RHAH_Settings settings;
        int revision = -1, mode = -1;
        object language;
        string allLabel, noneLabel, resetLabel, searchLabel, emptyLabel, sortLabel, sectionLabel, quoteLabel;

        internal RHAH_IrisMenusItemList(RHAH_IrisMenusItemKind kind)
        {
            this.kind = kind;
            switch (kind)
            {
                case RHAH_IrisMenusItemKind.Give:
                    rootAnchor = "give-foods"; itemPrefix = "give-food-"; translation = "RHAH_Settings_GiveFoods"; sortAnchor = "give-food-sort"; break;
                case RHAH_IrisMenusItemKind.Beg:
                    rootAnchor = "beg-foods"; itemPrefix = "beg-food-"; translation = "RHAH_Settings_BegFoods"; sortAnchor = "beg-food-sort"; break;
                case RHAH_IrisMenusItemKind.Demand:
                    rootAnchor = "demand-foods"; itemPrefix = "demand-food-"; translation = "RHAH_Settings_DemandFoods"; sortAnchor = "demand-food-sort"; break;
                case RHAH_IrisMenusItemKind.Apparel:
                    rootAnchor = "refugee-apparel"; itemPrefix = "refugee-apparel-"; translation = "RHAH_Settings_RefugeeApparel"; sortAnchor = "refugee-apparel-sort"; break;
                default:
                    rootAnchor = "relief-foods"; itemPrefix = "relief-food-"; translation = "RHAH_Settings_ReliefFoods"; break;
            }
            view = new MenuThingsList(itemPrefix, itemPrefix + (kind == RHAH_IrisMenusItemKind.Relief ? "mod-" : "group-"));
            searchAnchor = itemPrefix + "search";
            searchControl = itemPrefix + "query";
            noteAnchor = rootAnchor + "-note";
            allAnchor = itemPrefix + "all";
            noneAnchor = itemPrefix + "none";
            resetAnchor = itemPrefix + "reset";
            selected = IsSelected;
            assign = SetSelected;
            assignMode = SetMode;
            choices = new RHAH_IrisMenusChoiceLabels(translation + "_Sort", 3);
        }

        int CurrentMode
        {
            get
            {
                switch (kind)
                {
                    case RHAH_IrisMenusItemKind.Give: return settings.giveFoodListMode;
                    case RHAH_IrisMenusItemKind.Beg: return settings.begFoodListMode;
                    case RHAH_IrisMenusItemKind.Demand: return settings.demandFoodListMode;
                    case RHAH_IrisMenusItemKind.Apparel: return settings.apparelListMode;
                    default: return 0;
                }
            }
        }

        internal void Draw(Listing_Standard list, RHAH_Settings settings, ref string query, HashSet<string> collapsed, ref string pending)
        {
            this.settings = settings;
            Prepare();
            if (pending != null) query = string.Empty;
            MenuControls.Anchor(list, rootAnchor, 120f);
            if (kind == RHAH_IrisMenusItemKind.Demand) RHAH_IrisMenusWidgets.Section(list, sectionLabel);
            RHAH_IrisMenusWidgets.Quote(list, noteAnchor, quoteLabel);
            if (sortAnchor != null) MenuItemControls.ChoiceSort(list, sortAnchor, sortLabel, CurrentMode, choices.Options, assignMode);
            DrawBulk(list);
            MenuControls.Anchor(list, searchAnchor, 28f);
            Rect search = list.GetRect(28f);
            query = RHAH_IrisMenusWidgets.TextField(search, searchControl, query ?? string.Empty, searchLabel);
            list.Gap(4f);
            if (mode != CurrentMode) PrepareGroups();
            view.Draw(list, query, collapsed, ref pending, selected, assign, emptyLabel);
        }

        void Prepare()
        {
            bool changedLanguage = !ReferenceEquals(language, LanguageDatabase.activeLanguage);
            if (revision != ThingRegistry.Revision)
            {
                revision = ThingRegistry.Revision;
                candidates.Clear();
                switch (kind)
                {
                    case RHAH_IrisMenusItemKind.Beg: RHAH_ReliefFood.AppendBegFoods(candidates); break;
                    case RHAH_IrisMenusItemKind.Demand: RHAH_DemandFood.AppendCandidates(candidates); break;
                    case RHAH_IrisMenusItemKind.Apparel: RHAH_ApparelAssigner.AppendCandidates(candidates); break;
                    default: RHAH_ReliefFood.AppendCandidateFoods(candidates); break;
                }
                names.Clear();
                for (int i = 0; i < candidates.Count; i++) names.Add(candidates[i].defName);
                changedLanguage = true;
            }
            if (changedLanguage)
            {
                language = LanguageDatabase.activeLanguage;
                entries.Clear();
                for (int i = 0; i < candidates.Count; i++)
                {
                    ThingDef def = candidates[i];
                    string description = string.Empty;
                    if (kind == RHAH_IrisMenusItemKind.Demand)
                    {
                        float nutrition = def.GetStatValueAbstract(StatDefOf.Nutrition);
                        description = "RHAH_Settings_DemandFoods_Row".Translate(def.LabelCap, nutrition.ToString("0.###"), (nutrition / 0.9f).ToString("0.###"));
                    }
                    entries.Add(def, new UiThingEntry(def, description: description, tooltip: kind == RHAH_IrisMenusItemKind.Apparel ? "RHAH_Settings_RefugeeApparel_Tooltip".Translate().ToString() : null));
                }
                allLabel = (translation + "_All").Translate();
                noneLabel = (translation + "_None").Translate();
                resetLabel = kind == RHAH_IrisMenusItemKind.Demand ? (translation + "_Reset").Translate().ToString() : null;
                searchLabel = (translation + "_Search").Translate();
                emptyLabel = (translation + "_Empty").Translate();
                sortLabel = (translation + "_Sort").Translate();
                sectionLabel = translation.Translate();
                quoteLabel = (translation + "_Quote").Translate();
                PrepareGroups();
            }
        }

        void PrepareGroups()
        {
            mode = CurrentMode;
            List<List<ThingDef>> source = RHAH_ReliefFood.GroupFoods(mode, candidates);
            var groups = new List<UiThingsGroup>(source.Count);
            for (int i = 0; i < source.Count; i++)
            {
                if (source[i].Count == 0) continue;
                string key = RHAH_ReliefFood.GroupKey(mode, source[i][0]);
                string title = RHAH_ReliefFood.GroupTitle(mode, key) ?? (mode == 2 ? "RHAH_Settings_GiveFoods_Other".Translate().ToString() : "RHAH_Menu_Genes_UnknownMod".Translate().ToString());
                var group = new List<UiThingEntry>(source[i].Count);
                for (int j = 0; j < source[i].Count; j++) group.Add(entries[source[i][j]]);
                groups.Add(new UiThingsGroup(key, title, group));
            }
            view.SetGroups(groups.AsReadOnly());
        }

        void DrawBulk(Listing_Standard list)
        {
            MenuControls.Anchor(list, allAnchor, 28f);
            MenuControls.Anchor(list, noneAnchor, 28f);
            if (resetLabel != null) MenuControls.Anchor(list, resetAnchor, 28f);
            Rect row = list.GetRect(28f);
            int count = resetLabel == null ? 2 : 3;
            float width = (row.width - 8f * (count - 1)) / count;
            if (RHAH_IrisMenusWidgets.IsVisible(row))
            {
                if (RHAH_IrisMenusWidgets.Button(new Rect(row.x, row.y, width, 26f), allAnchor, allLabel)) SetAll(true);
                if (RHAH_IrisMenusWidgets.Button(new Rect(row.x + width + 8f, row.y, width, 26f), noneAnchor, noneLabel)) SetAll(false);
                if (resetLabel != null && RHAH_IrisMenusWidgets.Button(new Rect(row.x + (width + 8f) * 2f, row.y, width, 26f), resetAnchor, resetLabel)) settings.ResetDemandFoods();
            }
            list.Gap(4f);
        }

        bool IsSelected(UiThingEntry entry)
        {
            switch (kind)
            {
                case RHAH_IrisMenusItemKind.Give: return settings.IsGiveFoodEnabled(entry.Def.defName);
                case RHAH_IrisMenusItemKind.Beg: return settings.IsBegFoodEnabled(entry.Def.defName);
                case RHAH_IrisMenusItemKind.Demand: return settings.IsDemandFoodEnabled(entry.Def);
                case RHAH_IrisMenusItemKind.Apparel: return settings.IsRefugeeApparelEnabled(entry.Def.defName);
                default: return settings.IsReliefFoodEnabled(entry.Def.defName);
            }
        }

        void SetSelected(UiThingEntry entry, bool value)
        {
            switch (kind)
            {
                case RHAH_IrisMenusItemKind.Give: settings.SetGiveFoodEnabled(entry.Def.defName, value); break;
                case RHAH_IrisMenusItemKind.Beg: settings.SetBegFoodEnabled(entry.Def.defName, value); break;
                case RHAH_IrisMenusItemKind.Demand: settings.SetDemandFoodEnabled(entry.Def.defName, value); break;
                case RHAH_IrisMenusItemKind.Apparel: settings.SetRefugeeApparelEnabled(entry.Def.defName, value); break;
                default: settings.SetReliefFoodEnabled(entry.Def.defName, value); break;
            }
        }

        void SetAll(bool value)
        {
            switch (kind)
            {
                case RHAH_IrisMenusItemKind.Give: settings.SetAllGiveFood(value, value ? null : names); break;
                case RHAH_IrisMenusItemKind.Beg: settings.SetAllBegFood(value, value ? null : names); break;
                case RHAH_IrisMenusItemKind.Demand: settings.SetAllDemandFood(value, candidates); break;
                case RHAH_IrisMenusItemKind.Apparel: settings.SetAllRefugeeApparel(value, value ? null : names); break;
                default: settings.SetAllReliefFood(value, value ? null : names); break;
            }
        }

        void SetMode(int value)
        {
            switch (kind)
            {
                case RHAH_IrisMenusItemKind.Give: settings.giveFoodListMode = value; break;
                case RHAH_IrisMenusItemKind.Beg: settings.begFoodListMode = value; break;
                case RHAH_IrisMenusItemKind.Demand: settings.demandFoodListMode = value; break;
                case RHAH_IrisMenusItemKind.Apparel: settings.apparelListMode = value; break;
            }
        }
    }
}
#endif
