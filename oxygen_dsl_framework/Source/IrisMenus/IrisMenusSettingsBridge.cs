using System;
using System.Collections.Generic;
using System.Globalization;
using IrisMenus;
using Oxygendsl.Core;
using Oxygendsl.Mod;
using UnityEngine;
using Verse;

namespace Oxygendsl.IrisMenus
{
    [StaticConstructorOnStartup]
    public static class IrisMenusSettingsBootstrap
    {
        static IrisMenusSettingsBootstrap() { OxygenSettingsHost.InstallBridge(new IrisMenusSettingsBridge()); }
    }

    public sealed class IrisMenusSettingsBridge : IOxygenSettingsBridge
    {
        readonly Dictionary<Verse.Mod, List<PageBinding>> bindings = new Dictionary<Verse.Mod, List<PageBinding>>();

        public void Replace(Verse.Mod owner, OxygenSettingsPlan plan, IOxygenSettingsValues values)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (values == null) throw new ArgumentNullException(nameof(values));
            var next = new List<PageBinding>();
            var listings = new List<MenuSubItemListing>();
            foreach (OxygenSettingsPage page in plan.Pages)
            {
                var binding = new PageBinding(page, values);
                next.Add(binding);
                listings.Add(new MenuSubItemListing(page.Id, page.Title, binding.Draw,
                    page.Scope == "global" ? (Action)owner.WriteSettings : () => { }, binding.Search));
            }
            // IrisMenus validates the whole replacement before touching live pages.
            MenuRegistry.ReplaceSubItemListings(owner, "oxygen.settings", listings);
            if (bindings.TryGetValue(owner, out List<PageBinding> previous))
                foreach (PageBinding binding in previous) binding.Active = false;
            bindings[owner] = next;
        }

        sealed class PageBinding
        {
            readonly OxygenSettingsPage page;
            readonly IOxygenSettingsValues values;
            readonly Dictionary<string, string> numberBuffers = new Dictionary<string, string>(StringComparer.Ordinal);
            readonly Dictionary<string, object> bufferedValues = new Dictionary<string, object>(StringComparer.Ordinal);
            public bool Active = true;

            public PageBinding(OxygenSettingsPage page, IOxygenSettingsValues values) { this.page = page; this.values = values; }
            bool Available { get { return Active && (page.Scope != "game" || values.GameAvailable); } }
            bool Interactive(OxygenSettingsItem item) { return Available && item.Visible() && item.Enabled(); }

            public IEnumerable<MenuSearchEntry> Search()
            {
                foreach (OxygenSettingsItem item in page.Items)
                    if (item.Visible()) yield return new MenuSearchEntry(item.Id, item.Label, item.Description, page.Title);
            }

            public void Draw(Listing_Standard list)
            {
                if (!Active) return;
                if (!Available) list.Label("Oxygendsl_SettingsNeedGame".Translate());
                foreach (OxygenSettingsItem item in page.Items)
                {
                    if (!item.Visible()) continue;
                    MenuControls.Anchor(list, item.Id);
                    string label = item.Label();
                    string description = item.Description();
                    bool oldEnabled = GUI.enabled;
                    try
                    {
                        GUI.enabled = oldEnabled && Available && item.Enabled();
                        switch (item.Kind)
                        {
                            case "label": list.Label(label); break;
                            case "section": MenuControls.Section(list, label); break;
                            case "button": if (list.ButtonText(label) && Interactive(item)) item.Action(); break;
                            default: DrawValue(list, item, label, description); break;
                        }
                        if (!string.IsNullOrEmpty(description)) list.Label(description);
                    }
                    finally { GUI.enabled = oldEnabled; }
                }
            }

            void DrawValue(Listing_Standard list, OxygenSettingsItem item, string label, string description)
            {
                object current = Available ? values.Get(item.Key, page.Scope, item.Default) : item.Default;
                object next = current;
                switch (item.Kind)
                {
                    case "checkbox":
                        if (!(current is bool)) throw InvalidValue(item);
                        bool flag = (bool)current;
                        MenuControls.Checkbox(list, label, ref flag, description);
                        next = flag;
                        break;
                    case "number":
                        if (!(current is long || current is int || current is double || current is float)) throw InvalidValue(item);
                        string buffer;
                        if (!bufferedValues.TryGetValue(item.Id, out object buffered) || !Equals(buffered, current))
                            buffer = Convert.ToString(current, CultureInfo.InvariantCulture);
                        else numberBuffers.TryGetValue(item.Id, out buffer);
                        if (item.Default is long || item.Default is int)
                        {
                            double number = Convert.ToDouble(current, CultureInfo.InvariantCulture);
                            if (number != Math.Truncate(number) || number < int.MinValue || number > int.MaxValue) throw InvalidValue(item);
                            int integer = (int)number;
                            MenuControls.Number(list, label, ref integer, ref buffer, (int)item.Min, (int)item.Max);
                            if (integer != (int)number) next = integer;
                        }
                        else
                        {
                            float original = FiniteNumber(current, item);
                            float number = original;
                            MenuControls.Number(list, label, ref number, ref buffer, (float)item.Min, (float)item.Max);
                            if (number != original) next = (double)number;
                        }
                        numberBuffers[item.Id] = buffer;
                        bufferedValues[item.Id] = next;
                        break;
                    case "slider":
                        float old = FiniteNumber(current, item);
                        float selected = MenuControls.Slider(list, label, old, (float)item.Min, (float)item.Max, tooltip: description);
                        if (selected != old) next = (double)selected;
                        break;
                    case "text":
                        if (!(current is string)) throw InvalidValue(item);
                        list.Label(label);
                        next = list.TextEntry((string)current);
                        break;
                    case "select":
                        if (!(current is string)) throw InvalidValue(item);
                        IReadOnlyList<OxygenSettingsOption> options = item.Options();
                        OxygenSettingsOption chosen = null;
                        foreach (OxygenSettingsOption option in options) if (option.Value == (string)current) { chosen = option; break; }
                        string chosenLabel = chosen == null ? (string)current : chosen.Label;
                        list.Label(label);
                        if (list.ButtonText(chosenLabel) && Interactive(item))
                        {
                            var menu = new List<FloatMenuOption>();
                            foreach (OxygenSettingsOption option in options)
                            {
                                string optionValue = option.Value;
                                menu.Add(new FloatMenuOption(option.Label, () => Select(item, optionValue)));
                            }
                            if (menu.Count > 0) Find.WindowStack.Add(new FloatMenu(menu));
                        }
                        return;
                }
                if (!Same(current, next) && Interactive(item)) Write(item, next);
            }

            void Select(OxygenSettingsItem item, string value)
            {
                try
                {
                    if (!Interactive(item)) return;
                    // FloatMenu callbacks can run after a reload or a save switch; recheck current options too.
                    foreach (OxygenSettingsOption option in item.Options())
                        if (option.Value == value) { Write(item, value); return; }
                }
                catch (Exception exception) { Log.Error("[Oxygendsl] Settings selection failed: " + exception.Message); }
            }

            void Write(OxygenSettingsItem item, object value)
            {
                values.Set(item.Key, page.Scope, value);
                item.Changed?.Invoke(values.Get(item.Key, page.Scope, item.Default));
                MenuRegistry.InvalidateSearchIndex();
            }

            static float FiniteNumber(object value, OxygenSettingsItem item)
            {
                if (!(value is long || value is int || value is double || value is float)) throw InvalidValue(item);
                double number = Convert.ToDouble(value, CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number) || number < -float.MaxValue || number > float.MaxValue) throw InvalidValue(item);
                return (float)number;
            }
            static bool Same(object left, object right)
            {
                if ((left is long || left is int || left is double || left is float) && (right is long || right is int || right is double || right is float))
                    return Convert.ToDouble(left, CultureInfo.InvariantCulture) == Convert.ToDouble(right, CultureInfo.InvariantCulture);
                return Equals(left, right);
            }
            static InvalidOperationException InvalidValue(OxygenSettingsItem item)
            { return new InvalidOperationException("Invalid saved value for settings key '" + item.Key + "'."); }
        }
    }
}
