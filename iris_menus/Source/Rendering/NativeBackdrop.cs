using System;
using IrisMenus.UI;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    // The reference's three stops are opaque; only the host's entrance/exit fades them.
    internal sealed class NativeBackdrop : IDisposable
    {
        private const int Height = 257;
        private readonly Color[] pixels = new Color[Height];
        private Texture2D texture;
        private float lastDark = -1f;
        private Texture2D print;
        private string marquee;
        private float marqueeWidth, marqueeFont;
        private double lastTime;
        private float offset;

        internal void Draw(UiContext context, Rect rect, string text)
        {
            if (!context.IsRepaint || rect.width <= 0f || rect.height <= 0f) return;
            float dark = Mathf.InverseLerp(UiPalette.Light.Ink.r, UiPalette.Dark.Ink.r, context.Palette.Ink.r);
            if (texture == null)
            {
                texture = new Texture2D(1, Height, TextureFormat.RGBA32, false)
                {
                    name = "IrisMenus native gradient",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear
                };
            }
            if (lastDark != dark)
            {
                lastDark = dark;
                Color top = Color.Lerp(Rgb(0xb8b8b8), Rgb(0x202326), dark);
                Color middle = Color.Lerp(Rgb(0xd8d8d8), Rgb(0x292c2f), dark);
                Color bottom = Color.Lerp(Rgb(0x8e8e8e), Rgb(0x111315), dark);
                for (int y = 0; y < Height; y++)
                {
                    float t = y / (float)(Height - 1);
                    pixels[y] = t <= 0.5f ? Color.Lerp(bottom, middle, t * 2f) : Color.Lerp(middle, top, t * 2f - 1f);
                }
                texture.SetPixels(pixels);
                texture.Apply(false, false);
            }
            Color previous = GUI.color;
            try
            {
                GUI.color = new Color(1f, 1f, 1f, context.Alpha);
                GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
                if (print == null) print = ContentFinder<Texture2D>.Get("IrisMenus/UI/PlanarPrint");
                float scale = context.Scale;
                Color ink = context.Palette.Ink;
                GUI.color = new Color(ink.r, ink.g, ink.b, context.Alpha * 0.095f);
                float height = Mathf.Min(360f * scale, rect.height * 0.45f);
                GUI.DrawTexture(new Rect(rect.x, rect.yMax - 40f * scale - height, height * 1.18f, height), print, ScaleMode.StretchToFill, true);
                context.Fill(new Rect(rect.x, rect.y + 116f * scale, 6f * scale, 64f * scale), Faint(ink, 0.07f));
                DrawMarquee(context, rect, text, scale);
            }
            finally { GUI.color = previous; }
        }

        private void DrawMarquee(UiContext context, Rect rect, string text, float scale)
        {
            float previousFont = context.FontSize;
            FontStyle previousStyle = context.FontStyle;
            try
            {
                context.FontSize = 104f * scale;
                context.FontStyle = FontStyle.Bold;
                if (marquee != text || marqueeFont != context.FontSize)
                {
                    marquee = text; marqueeFont = context.FontSize;
                    marqueeWidth = UiTextMetrics.Width(context, marquee);
                    offset = 0f;
                }
                float elapsed = lastTime == 0d ? 0f : (float)Math.Max(0d, context.Time - lastTime);
                lastTime = context.Time;
                if (marqueeWidth <= 0f) return;
                float pitch = marqueeWidth + 48f * scale;
                if (!context.ReducedMotion) offset = (offset + elapsed * 16f * scale) % pitch;
                Rect area = new Rect(rect.x, rect.yMax - 176f * scale,
                    rect.width, 128f * scale);
                using (context.Clip(area))
                {
                    Color color = Faint(context.Palette.Ink, 0.18f);
                    float x = context.ReducedMotion ? 0f : -offset;
                    context.Label(new Rect(x, 0f, marqueeWidth, area.height), marquee, color);
                    if (!context.ReducedMotion)
                        for (x += pitch; x < area.width; x += pitch)
                            context.Label(new Rect(x, 0f, marqueeWidth, area.height), marquee, color);
                }
            }
            finally { context.FontSize = previousFont; context.FontStyle = previousStyle; }
        }

        private static Color Faint(Color color, float alpha) { color.a = alpha; return color; }

        private static Color Rgb(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public void Dispose()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
