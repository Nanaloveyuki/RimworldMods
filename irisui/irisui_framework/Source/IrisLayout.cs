using System;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisUI.Framework
{
    public enum InspectorMode { Minimum, Normal, Fullscreen }

    public readonly struct InspectorLayoutRequest
    {
        public readonly bool Active;
        public readonly InspectorMode Mode;
        public readonly Vector2 NativeSize;
        public InspectorLayoutRequest(bool active, InspectorMode mode, Vector2 nativeSize)
        { Active = active; Mode = mode; NativeSize = nativeSize; }
    }

    public readonly struct UiLayout
    {
        public readonly Rect Viewport, ColonistBar, Inspector, Navigation, Body, Portrait;
        public readonly bool InspectorActive, Fullscreen;
        public readonly InspectorMode Mode;
        public readonly float Scale;
        public readonly int Version;
        internal UiLayout(Rect viewport, Rect bar, Rect inspector, Rect navigation, Rect body, Rect portrait,
            InspectorLayoutRequest request, float scale, int version)
        {
            Viewport = viewport; ColonistBar = bar; Inspector = inspector; Navigation = navigation;
            Body = body; Portrait = portrait; InspectorActive = request.Active;
            Fullscreen = request.Active && request.Mode == InspectorMode.Fullscreen;
            Mode = request.Mode; Scale = scale; Version = version;
        }
    }

    public static class IrisLayout
    {
        private static Func<InspectorLayoutRequest> inspector;
        private static UiLayout current;
        private static InspectorLayoutRequest previous;
        private static int width, height, version;
        private static float scale;
        private static Rect occupiedBar;
        private static int occupiedFrame = -1;
        public static void RegisterInspector(Func<InspectorLayoutRequest> getLayout) => inspector = getLayout;
        public static UiTheme Theme => FrameworkMod.Settings.Theme;
        public static bool ReducedMotion => FrameworkMod.Settings.ReducedMotion;

        public static UiLayout Current
        {
            get
            {
                InspectorLayoutRequest request = inspector == null ? default : inspector();
                var settings = FrameworkMod.Settings;
                Rect bar = NativeColonistBarBounds();
                if (width != UI.screenWidth || height != UI.screenHeight || scale != settings.Scale || occupiedBar != bar ||
                    previous.Active != request.Active || previous.Mode != request.Mode || previous.NativeSize != request.NativeSize)
                {
                    width = UI.screenWidth; height = UI.screenHeight; scale = settings.Scale; occupiedBar = bar;
                    previous = request;
                    current = Calculate(new Rect(0f, 0f, width, height), request, scale, bar, ++version);
                }
                return current;
            }
        }

        public static UiLayout Calculate(Rect viewport, InspectorLayoutRequest request, float scale, Rect bar, int version = 0)
        {
            float gap = 12f * scale;
            bool full = request.Active && request.Mode == InspectorMode.Fullscreen;
            float compactHeight = (request.Mode == InspectorMode.Minimum ? 148f : 224f) * scale;
            float paneWidth = Mathf.Min(420f * scale, viewport.width - 220f * scale);
            Rect pane = request.Active
                ? new Rect(viewport.x + gap, viewport.yMax - 35f - gap - compactHeight, paneWidth, compactHeight)
                : default;
            Rect navigation = default, body = default, portrait = default;
            if (request.Active && !full && request.Mode != InspectorMode.Minimum)
            {
                navigation = new Rect(pane.x, pane.y - 38f * scale, pane.width, 32f * scale);
                if (request.NativeSize != Vector2.zero)
                {
                    float bodyWidth = Mathf.Min(request.NativeSize.x, viewport.width - 2f * gap);
                    float top = Mathf.Max(viewport.y, bar.yMax) + gap;
                    float bodyHeight = Mathf.Min(request.NativeSize.y, Mathf.Max(80f, navigation.y - top - gap));
                    body = new Rect(pane.x, navigation.y - gap - bodyHeight, bodyWidth, bodyHeight);
                }
            }
            if (full)
            {
                bar = default;
                pane = viewport;
                float railWidth = 76f * scale;
                navigation = new Rect(viewport.xMax - railWidth, viewport.y + 96f * scale, railWidth, viewport.height - 120f * scale);
                float availableWidth = Mathf.Max(80f, viewport.width - railWidth - gap * 5f);
                float bodyWidth = Mathf.Min(request.NativeSize == Vector2.zero ? 420f * scale : request.NativeSize.x, availableWidth * 0.58f);
                float bodyHeight = Mathf.Min(request.NativeSize == Vector2.zero ? 330f * scale : request.NativeSize.y, viewport.height - 160f * scale);
                body = new Rect(viewport.x + gap * 2f, viewport.center.y - bodyHeight * 0.5f + 30f * scale, bodyWidth, bodyHeight);
                portrait = new Rect(body.xMax + gap, viewport.y + 100f * scale,
                    Mathf.Max(80f, navigation.x - body.xMax - gap * 2f), viewport.height - 130f * scale);
            }
            return new UiLayout(viewport, bar, pane, navigation, body, portrait, request, scale, version);
        }

        private static Rect measuredBar;
        private static Rect NativeColonistBarBounds()
        {
            if (CurrentGameInactive || !Find.PlaySettings.showColonistBar || Find.TilePicker.Active) return default;
            if (occupiedFrame == Time.frameCount) return measuredBar;
            occupiedFrame = Time.frameCount;
            measuredBar = default;
            var locations = Find.ColonistBar.DrawLocs;
            Vector2 size = Find.ColonistBar.Size;
            for (int i = 0; i < locations.Count; i++)
            {
                Rect entry = new Rect(locations[i].x, locations[i].y, size.x, size.y);
                if (i == 0) measuredBar = entry;
                else
                {
                    measuredBar.xMin = Mathf.Min(measuredBar.xMin, entry.xMin);
                    measuredBar.yMin = Mathf.Min(measuredBar.yMin, entry.yMin);
                    measuredBar.xMax = Mathf.Max(measuredBar.xMax, entry.xMax);
                    measuredBar.yMax = Mathf.Max(measuredBar.yMax, entry.yMax);
                }
            }
            return measuredBar;
        }

        private static bool CurrentGameInactive => Verse.Current.ProgramState != ProgramState.Playing || Verse.Current.Game == null;

        public static bool ColonistBarVisible => !Current.Fullscreen;

        public static UiInputEligibility InputFor(Window owner, Rect region, bool keyboard)
        {
            WindowStack stack = Find.WindowStack;
            if (!stack.GetsInput(owner)) return UiInputEligibility.None;
            Window underMouse = stack.GetWindowAt(UI.MousePositionOnUIInverted);
            bool pointer = underMouse == owner || underMouse == null;
            return (pointer ? UiInputEligibility.Pointer : UiInputEligibility.None) |
                (keyboard ? UiInputEligibility.Keyboard : UiInputEligibility.None);
        }
    }
}
