using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiSideRail
    {
        private readonly UiNavigation navigation;

        public UiSideRail(UiContext context, string[] labels, string[] keys = null)
        { navigation = new UiNavigation(context, labels, keys); }
        public int Count => navigation.Count;
        public float Visibility => navigation.Visibility;
        public UiId ItemId(int index) => navigation.ItemId(index);
        public void SetItems(string[] labels, string[] keys = null) => navigation.SetItems(labels, keys);
        public float NaturalHeight(float rowHeight = 44f, float gap = 4f)
            => Count == 0 ? 0f : Count * rowHeight + (Count - 1) * gap;

        public int Draw(Rect rect, int selected, bool enabled = true, bool[] itemEnabled = null,
            float rowHeight = 44f, float gap = 4f, bool visible = true, Texture2D[] icons = null,
            Rect[] itemRects = null)
            => navigation.Draw(rect, selected, true, rowHeight, gap, enabled, itemEnabled, visible, icons,
                itemRects: itemRects);
    }
}
