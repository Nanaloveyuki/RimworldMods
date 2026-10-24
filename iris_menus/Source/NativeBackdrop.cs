using System;
using IrisMenus.UI;
using UnityEngine;

namespace IrisMenus
{
    // The reference's three stops are opaque; only the host's entrance/exit fades them.
    internal sealed class NativeBackdrop : IDisposable
    {
        private const int Height = 257;
        private readonly Color[] pixels = new Color[Height];
        private Texture2D texture;
        private float lastDark = -1f;

        internal void Draw(UiContext context, Rect rect)
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
            }
            finally { GUI.color = previous; }
        }

        private static Color Rgb(int rgb) => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f);

        public void Dispose()
        {
            if (texture != null) UnityEngine.Object.Destroy(texture);
            texture = null;
        }
    }
}
