using HarmonyLib;
using UnityEngine;
using Verse;

namespace IrisUI.Framework
{
    [HarmonyPatch(typeof(GizmoGridDrawer), nameof(GizmoGridDrawer.DrawGizmoGrid))]
    internal static class NativeCommandLayout
    {
        private static void Prefix(ref float startX)
        {
            UiLayout layout = IrisLayout.Current;
            if (layout.InspectorActive && !layout.Fullscreen)
                startX = layout.Inspector.xMax + 14f;
        }
    }
}
