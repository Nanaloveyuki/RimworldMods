using System;
using HarmonyLib;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal sealed partial class HostedGuiTheme : IDisposable
    {
        [ThreadStatic] private static HostedGuiTheme current;
        private UiContext context;
        private UiPalette palette;
        private float alpha;
        private bool bypass = false;

        internal static void Install(Harmony harmony)
        {
            #if RIMWORLD_1_7
            InstallText(harmony);
            InstallControls(harmony);
            InstallHoverCards(harmony);
            #endif
        }

        internal Scope Enter(UiContext ui, string identity)
        {
            HostedGuiTheme previous = current;
            context = ui;
            palette = ui.Palette;
            alpha = ui.Alpha;
            BeginControls(identity);
            BeginHoverCards();
            current = this;
            return new Scope(this, previous);
        }

        public void Dispose()
        {
            DisposeHoverCards();
            DisposeControls();
            if (current == this) current = null;
            context = null;
        }

        internal readonly struct Scope : IDisposable
        {
            private readonly HostedGuiTheme owner, previous;
            internal Scope(HostedGuiTheme owner, HostedGuiTheme previous) { this.owner = owner; this.previous = previous; }
            public void Dispose()
            {
                if (owner == null) return;
                owner.EndHoverCards();
                current = previous;
            }
        }


        private void Background(Rect rect, Color fill, Color border)
        {
            Color saved = GUI.color;
            try
            {
                fill.a *= alpha;
                border.a *= alpha;
                GUI.color = fill;
                GUI.DrawTexture(rect, BaseContent.WhiteTex);
                GUI.color = border;
                Widgets.DrawBox(rect);
            }
            finally { GUI.color = saved; }
        }

        private static Color MapBackground(Color color)
        {
            if (current == null || current.bypass) return color;
            Color target;
            if (SameRgb(color, Widgets.WindowBGFillColor)) target = current.palette.Surface;
            else if (SameRgb(color, Widgets.MenuSectionBGFillColor)) target = current.palette.Sunken;
            else return color;
            target.a = color.a * current.alpha;
            return target;
        }

        private static bool SameRgb(Color a, Color b) => Mathf.Abs(a.r - b.r) < 0.001f &&
            Mathf.Abs(a.g - b.g) < 0.001f && Mathf.Abs(a.b - b.b) < 0.001f;

        [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawWindowBackground), new[] { typeof(Rect) })]
        private static class WindowBackground
        {
            private static bool Prefix(Rect rect)
            {
                if (current == null || current.bypass) return true;
                current.Background(rect, current.palette.Surface, current.palette.StrongLine);
                return false;
            }
        }

        [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawWindowBackground), new[] { typeof(Rect), typeof(Color) })]
        private static class FactoredWindowBackground
        {
            private static bool Prefix(Rect rect, Color colorFactor)
            {
                if (current == null || current.bypass) return true;
                current.Background(rect, current.palette.Surface * colorFactor, current.palette.StrongLine * colorFactor);
                return false;
            }
        }

        [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawMenuSection))]
        private static class MenuBackground
        {
            private static bool Prefix(Rect rect)
            {
                if (current == null || current.bypass) return true;
                current.Background(rect, current.palette.Sunken, current.palette.StrongLine);
                return false;
            }
        }


        [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawBoxSolid))]
        private static class SolidBackground
        {
            private static void Prefix(ref Color color) { color = MapBackground(color); }
        }

        [HarmonyPatch(typeof(Widgets), nameof(Widgets.DrawRectFast))]
        private static class FastBackground
        {
            private static void Prefix(ref Color color) { color = MapBackground(color); }
        }
    }
}
