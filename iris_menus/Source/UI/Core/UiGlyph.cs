using UnityEngine;
using Verse;

namespace IrisMenus.UI
{
    public enum UiGlyphKind { None, Search, Close, Sun, Moon, Plus, ArrowRight, ChevronRight, Help, Menu, Settings, Layers, Grid, Check, Reset, Pin, Unpin, Edit, ArrowUp, ArrowDown, Trash }

    [StaticConstructorOnStartup]
    public static class UiGlyph
    {
        private static readonly Texture2D[] textures;

        static UiGlyph()
        {
            textures = new Texture2D[]
            {
                null,
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/search"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/x"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/sun"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/moon"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/plus"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/arrow-right"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/chevron-right"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/circle-help"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/menu"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/sliders-horizontal"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/layers-2"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/grip"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/check"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/rotate-ccw"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/pin"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/pin-off"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/pencil"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/arrow-up"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/arrow-down"),
                ContentFinder<Texture2D>.Get("IrisMenus/UI/Glyphs/trash-2")
            };
        }

        public static void Draw(UiContext context, Rect rect, UiGlyphKind kind, Color color, float rotation = 0f)
        {
            int index = (int)kind;
            if (!context.IsRepaint || rect.width <= 0f || rect.height <= 0f || index <= 0 || index >= textures.Length) return;
            Texture2D texture = textures[index];
            if (texture == null) return;
            float size = Mathf.Min(rect.width, rect.height);
            Vector2 center = rect.center;
            Rect glyph = new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size);
            color.a *= context.Alpha;
#if RIMWORLD_1_7
            var backend = UnityStubs.Gui.GuiBackend.Current;
            if (backend == null) return;
            if (rotation == 0f)
            {
                backend.DrawTexture(glyph, texture, ScaleMode.StretchToFill, color);
                return;
            }
            // Rotate only the rendered quad; never alter the input/focus coordinate matrix.
            float radians = rotation * Mathf.Deg2Rad;
            float cosine = Mathf.Cos(radians) * size * 0.5f;
            float sine = Mathf.Sin(radians) * size * 0.5f;
            Vector2 right = new Vector2(cosine, sine);
            Vector2 down = new Vector2(-sine, cosine);
            backend.DrawTextureQuad(center - right - down, center + right - down,
                center + right + down, center - right + down, texture, color);
#else
            Color previousColor = GUI.color;
            Matrix4x4 previousMatrix = GUI.matrix;
            try
            {
                GUI.color = color;
                if (rotation != 0f) GUIUtility.RotateAroundPivot(rotation, center);
                GUI.DrawTexture(glyph, texture, ScaleMode.StretchToFill, true);
            }
            finally
            {
                GUI.matrix = previousMatrix;
                GUI.color = previousColor;
            }
#endif
        }
    }
}
