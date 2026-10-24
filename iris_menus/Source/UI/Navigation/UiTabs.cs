using UnityEngine;

namespace IrisMenus.UI
{
    public sealed class UiTabs
    {
        private readonly UiNavigation navigation;

        public UiTabs(UiContext context, string[] labels) { navigation = new UiNavigation(context, labels); }
        public int Count => navigation.Count;
        public float Visibility => navigation.Visibility;
        public UiId ItemId(int index) => navigation.ItemId(index);

        // Keep the caller-owned labels array in the same order for this instance's lifetime.
        // Replacing labels in place is safe; reordering destinations requires a new instance.
        public int Draw(Rect rect, int selected, bool enabled = true, bool[] itemEnabled = null, bool visible = true)
            => navigation.Draw(rect, selected, false, 44f, 4f, enabled, itemEnabled, visible);
    }
}
