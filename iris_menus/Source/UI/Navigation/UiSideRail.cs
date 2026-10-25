using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiSideRail
    {
        private readonly UiNavigation navigation;

        public UiSideRail(UiContext context, string[] labels) { navigation = new UiNavigation(context, labels); }
        public int Count => navigation.Count;
        public float Visibility => navigation.Visibility;
        public UiId ItemId(int index) => navigation.ItemId(index);
        public float NaturalHeight(float rowHeight = 44f, float gap = 4f)
            => Count == 0 ? 0f : Count * rowHeight + (Count - 1) * gap;

        // Counts and badges intentionally do not belong to this navigation primitive.
        public int Draw(Rect rect, int selected, bool enabled = true, bool[] itemEnabled = null,
            float rowHeight = 44f, float gap = 4f, bool visible = true, UiGlyphKind[] icons = null)
            => navigation.Draw(rect, selected, true, rowHeight, gap, enabled, itemEnabled, visible, icons);
    }
}
