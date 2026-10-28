using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace IrisUI.Framework
{
    internal static class ColonistBarVisibility
    {
        private static readonly List<Thing> EmptySelection = new List<Thing>();
        private static bool CanHit => IrisLayout.ColonistBarVisible && Find.PlaySettings.showColonistBar &&
            UI.screenWidth >= 800 && UI.screenHeight >= 500 && !Find.TilePicker.Active && Find.WindowStack.GetsInput(null);

        [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistBarOnGUI))]
        private static class Drawing
        {
            private static bool Prefix() => IrisLayout.ColonistBarVisible;
        }
        [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.TryGetEntryAt))]
        private static class PointHit
        {
            private static bool Prefix(Vector2 pos, ref bool __result, out ColonistBar.Entry entry)
            {
                entry = default;
                if (CanHit && Find.WindowStack.GetWindowAt(pos) == null) return true;
                __result = false;
                return false;
            }
        }
        [HarmonyPatch(typeof(ColonistBar), nameof(ColonistBar.ColonistsOrCorpsesInScreenRect))]
        private static class SelectionHit
        {
            private static bool Prefix(ref List<Thing> __result)
            {
                if (CanHit) return true;
                EmptySelection.Clear();
                __result = EmptySelection;
                return false;
            }
        }
    }
}
