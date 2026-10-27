#if RIMWORLD_1_7
using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace IrisMenus
{
    internal static class MainButtonVisibility
    {
        private static readonly Stack<List<MainButtonDef>> FilterLists = new Stack<List<MainButtonDef>>();
        private static bool installed;
        internal static List<string> Hidden => IrisMenusMod.Instance.Settings.HiddenMainButtons;
        internal static bool IsHidden(string defName) => Hidden.Contains(defName);

        internal static void Install()
        {
            if (installed) return;
            var harmony = new Harmony("Nanaloveyuki.IrisMenus.MainButtons");
            harmony.Patch(AccessTools.Method(typeof(MainButtonsRoot), "DoButtons"),
                prefix: new HarmonyMethod(typeof(MainButtonVisibility), nameof(BeginButtons)),
                finalizer: new HarmonyMethod(typeof(MainButtonVisibility), nameof(EndButtons)));
            installed = true;
        }

        internal static void SetHidden(string defName, bool hidden)
        {
            bool changed;
            if (hidden)
            {
                changed = !Hidden.Contains(defName);
                if (changed) Hidden.Add(defName);
            }
            else changed = Hidden.RemoveAll(value => value == defName) != 0;
            if (changed) IrisMenusMod.Instance.WriteSettings();
        }

        internal static void RestoreAll()
        {
            if (Hidden.Count == 0) return;
            Hidden.Clear();
            IrisMenusMod.Instance.WriteSettings();
        }

        private struct FilterState
        {
            internal List<MainButtonDef> Original;
            internal List<MainButtonDef> Filtered;
        }

        private static bool BeginButtons(ref List<MainButtonDef> ___allButtonsInOrder, out FilterState __state)
        {
            __state = default(FilterState);
            if (Hidden.Count == 0) return true;
            List<MainButtonDef> filtered = FilterLists.Count > 0 ? FilterLists.Pop() : new List<MainButtonDef>();
            // Set state before invoking mod workers so the finalizer also releases the scratch list on failure.
            __state.Original = ___allButtonsInOrder;
            __state.Filtered = filtered;
            bool anyVisible = false;
            for (int i = 0; i < ___allButtonsInOrder.Count; i++)
            {
                MainButtonDef def = ___allButtonsInOrder[i];
                if (IsHidden(def.defName)) continue;
                filtered.Add(def);
                if (def.Worker.Visible) anyVisible = true;
            }
            ___allButtonsInOrder = filtered;
            return anyVisible;
        }

        private static Exception EndButtons(Exception __exception, ref List<MainButtonDef> ___allButtonsInOrder, FilterState __state)
        {
            if (__state.Original != null)
            {
                ___allButtonsInOrder = __state.Original;
                __state.Filtered.Clear();
                FilterLists.Push(__state.Filtered);
            }
            return __exception;
        }

    }
}
#endif
