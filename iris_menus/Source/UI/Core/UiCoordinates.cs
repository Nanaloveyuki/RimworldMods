using UnityEngine;

namespace IrisMenus.UI
{
    internal static class UiCoordinates
    {
#if RIMWORLD_1_7
        private static Vector2 GroupOrigin()
        {
            var backend = UnityStubs.Gui.GuiBackend.Current;
            if (backend == null || backend.GroupCount == 0) return Vector2.zero;
            backend.GetGroup(backend.GroupCount - 1, out _, out Vector2 origin);
            return origin;
        }
#endif

        internal static Vector2 ToScreen(Vector2 point)
        {
#if RIMWORLD_1_7
            // 1.7's drawing path scales the group origin; GUIUtility's conversion does not.
            Vector2 origin = GroupOrigin();
            Vector3 transformed = GUI.matrix.MultiplyPoint3x4(new Vector3(point.x + origin.x, point.y + origin.y, 0f));
            return new Vector2(transformed.x, transformed.y);
#else
            return GUIUtility.GUIToScreenPoint(point);
#endif
        }

        internal static Vector2 FromScreen(Vector2 point)
        {
#if RIMWORLD_1_7
            Vector3 transformed = GUI.matrix.inverse.MultiplyPoint3x4(new Vector3(point.x, point.y, 0f));
            Vector2 origin = GroupOrigin();
            return new Vector2(transformed.x - origin.x, transformed.y - origin.y);
#else
            return GUIUtility.ScreenToGUIPoint(point);
#endif
        }

        internal static Rect ToScreen(Rect rect)
        {
            Vector2 min = ToScreen(rect.position);
            Vector2 max = ToScreen(new Vector2(rect.xMax, rect.yMax));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        internal static Rect FromScreen(Rect rect)
        {
            Vector2 min = FromScreen(rect.position);
            Vector2 max = FromScreen(new Vector2(rect.xMax, rect.yMax));
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }
    }
}
