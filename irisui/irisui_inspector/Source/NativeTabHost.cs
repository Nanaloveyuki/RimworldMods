using System;
using IrisUI.Framework;
using UnityEngine;
using Verse;

namespace IrisUI.Inspector
{
    // The original DoTabGUI callback remains responsible for validity, close and FillTab.
    internal static class NativeTabHost
    {
        private static readonly Action DrawCached = Draw;
        private static Action nativeDraw;
        private static InspectTabBase tab;
        private static Pawn pawn;
        private static Vector2 scroll, natural;
        internal static bool Matches(InspectTabBase active) => tab == active && pawn == InspectorController.SelectedPawn;

        internal static void Register(ref Rect rect, ref Action draw)
        {
            if (tab != InspectorController.ActiveTab || pawn != InspectorController.SelectedPawn)
            {
                tab = InspectorController.ActiveTab;
                pawn = InspectorController.SelectedPawn;
                scroll = Vector2.zero;
            }
            natural = rect.size;
            nativeDraw = draw;
            rect = IrisLayout.Current.Body;
            draw = DrawCached;
        }

        internal static void Clear()
        {
            nativeDraw = null;
            tab = null;
            pawn = null;
            scroll = Vector2.zero;
        }

        private static void Draw()
        {
            if (!InspectorController.IsActive || InspectorMod.Settings.Mode == InspectorMode.Minimum ||
                tab != InspectorController.ActiveTab || nativeDraw == null) return;
            Rect viewport = IrisLayout.Current.Body.AtZero();
            bool overflow = natural.x > viewport.width || natural.y > viewport.height;
            Color previous = GUI.color;
            Event evt = Event.current;
            EventType previousEvent = evt.type;
            bool hidden = InspectorSurface.ContentOpacity <= 0f;
            if (hidden && previousEvent == EventType.Repaint) return;
            bool previousEnabled = GUI.enabled;
            GUI.enabled = previousEnabled && !hidden;
            if (hidden && previousEvent != EventType.Layout && previousEvent != EventType.Repaint && previousEvent != EventType.Used)
                evt.type = EventType.Ignore;
            GUI.color = new Color(previous.r, previous.g, previous.b, previous.a * InspectorSurface.ContentOpacity);
            using (new TextBlock(GameFont.Small))
            try
            {
                if (!overflow)
                {
                    nativeDraw();
                    return;
                }
                // Account for both scrollbars before clamping, including the cross-axis scrollbar.
                bool vertical = natural.y > viewport.height;
                bool horizontal = natural.x > viewport.width - (vertical ? 16f : 0f);
                vertical |= natural.y > viewport.height - (horizontal ? 16f : 0f);
                float width = Mathf.Max(1f, viewport.width - (vertical ? 16f : 0f));
                float height = Mathf.Max(1f, viewport.height - (horizontal ? 16f : 0f));
                scroll.x = Mathf.Clamp(scroll.x, 0f, Mathf.Max(0f, natural.x - width));
                scroll.y = Mathf.Clamp(scroll.y, 0f, Mathf.Max(0f, natural.y - height));
                Widgets.BeginScrollView(viewport, ref scroll, new Rect(0f, 0f, natural.x, natural.y));
                try { nativeDraw(); }
                finally { Widgets.EndScrollView(); }
            }
            finally
            {
                GUI.color = previous;
                GUI.enabled = previousEnabled;
                if (hidden && previousEvent != EventType.Layout && previousEvent != EventType.Repaint && previousEvent != EventType.Used)
                    evt.type = previousEvent;
            }
        }
    }
}
