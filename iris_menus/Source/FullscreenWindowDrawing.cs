using UnityEngine;
using Verse;

namespace IrisMenus
{
    internal sealed class FullscreenWindowDrawing : IWindowDrawing
    {
        private readonly DefaultWindowDrawing vanilla = new DefaultWindowDrawing();

        public GUIStyle EmptyStyle => vanilla.EmptyStyle;
        // NativeSurface owns the full-screen background and its capture transition.
        public void DoWindowBackground(Rect rect) { }
        public bool DoCloseButton(Rect rect, string text) => vanilla.DoCloseButton(rect, text);
        public bool DoClostButtonSmall(Rect rect) => vanilla.DoClostButtonSmall(rect);
        public void BeginGroup(Rect rect) => vanilla.BeginGroup(rect);
        public void EndGroup() => vanilla.EndGroup();
        public void DoGrayOut(Rect rect) => vanilla.DoGrayOut(rect);
    }
}
