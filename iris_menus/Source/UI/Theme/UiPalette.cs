using UnityEngine;

namespace IrisMenus.UI
{
    public enum UiTheme { Light, Dark }

    public readonly struct UiPalette
    {
        public Color Ink { get; }
        public Color Muted { get; }
        public Color Surface { get; }
        public Color Raised { get; }
        public Color Sunken { get; }
        public Color Line { get; }
        public Color StrongLine { get; }
        public Color Hover { get; }
        public Color Selected { get; }
        public Color OnSelected { get; }
        public Color Signal { get; }
        public Color SwitchOn { get; }
        public Color Scrim { get; }
        public Color Footer { get; }
        public Color FooterInk { get; }
        public Color FooterLine { get; }
        public Color FooterMuted { get; }
        public Color Error { get; }
        public Color Input { get; }
        public Color Button { get; }
        public Color Sheet { get; }
        public Color Rail { get; }
        public Color SoftLine { get; }
        public Color Marker { get; }
        public Color Header { get; }
        public Color HeaderInk { get; }
        public Color Overlay { get; }
        public Color AccentInk { get; }
        public Color Preview { get; }
        public Color PreviewShade { get; }
        public Color PreviewEdge { get; }
        public Color Mini { get; }
        public Color MiniMark { get; }
        public Color MiniTrack { get; }
        public Color Shadow { get; }

        public static readonly UiPalette Light = new UiPalette(0f);
        public static readonly UiPalette Dark = new UiPalette(1f);

        private UiPalette(float dark)
        {
            Ink = Mix(0x252525, 0xebedef, dark);
            Muted = Mix(0x545454, 0xb3b8bc, dark);
            Surface = Mix(0xf5f5f5, 0x272a2e, dark);
            Raised = Mix(0xffffff, 0x303438, dark);
            Sunken = Mix(0xf2f2f2, 0x202326, dark);
            Line = Mix(0x000000, 0xffffff, dark, 28f / 255f, 32f / 255f);
            StrongLine = Mix(0x000000, 0xffffff, dark, 61f / 255f, 69f / 255f);
            SoftLine = Mix(0x000000, 0xffffff, dark, 16f / 255f, 18f / 255f);
            Hover = Mix(0xffffff, 0xffffff, dark, 84f / 255f, 15f / 255f);
            Selected = Mix(0x252525, 0xe6e9eb, dark);
            OnSelected = Mix(0xf5f5f5, 0x191c1e, dark);
            Signal = Rgb(0xf1ec58);
            SwitchOn = Mix(0x252525, 0x485058, dark);
            Scrim = Rgb(0x000000, UiMotion.Lerp(0.5f, 0.7f, dark));
            Footer = Mix(0x202326, 0x202326, dark, 1f, 0f);
            FooterInk = Rgb(0xebedef);
            FooterLine = Rgb(0xffffff, 69f / 255f);
            FooterMuted = Rgb(0xb3b8bc);
            Error = Mix(0xa72d32, 0xff989d, dark);
            Input = Mix(0xffffff, 0xffffff, dark, 72f / 255f, 12f / 255f);
            Button = Mix(0xffffff, 0x34383c, dark, 135f / 255f, 1f);
            Sheet = Mix(0xf7f7f7, 0x272a2e, dark, 222f / 255f, 242f / 255f);
            Rail = Mix(0xf2f2f2, 0x1a1d20, dark, 107f / 255f, 168f / 255f);
            Marker = Mix(0x8a8a8a, 0x8d959c, dark);
            Header = Rgb(0x1f1f1f);
            HeaderInk = Rgb(0xfafafa);
            Overlay = Mix(0xfafafa, 0x303438, dark);
            AccentInk = Mix(0xfffa00, 0x6b5d00, dark);
            Preview = Mix(0xb8b8b8, 0x141719, dark);
            PreviewShade = Mix(0x8f8f8f, 0x434a52, dark, 107f / 255f, 107f / 255f);
            PreviewEdge = Mix(0xffffff, 0xffffff, dark, 166f / 255f, 80f / 255f);
            Mini = Mix(0xf6f6f6, 0x30353a, dark, 214f / 255f, 232f / 255f);
            MiniMark = Mix(0x9c9c9c, 0x879099, dark);
            MiniTrack = Mix(0xb4b4b4, 0x5c646b, dark);
            Shadow = Mix(0x000000, 0x000000, dark, 20f / 255f, 96f / 255f);
        }

        public static UiPalette Blend(float dark) => new UiPalette(Mathf.Clamp01(dark));

        private static Color Mix(int light, int dark, float amount, float lightAlpha = 1f, float darkAlpha = 1f)
            => Color.Lerp(Rgb(light, lightAlpha), Rgb(dark, darkAlpha), amount);

        private static Color Rgb(int rgb, float alpha = 1f) => new Color(
            ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
    }
}
