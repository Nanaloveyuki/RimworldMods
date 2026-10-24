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
        public Color Error { get; }

        public static readonly UiPalette Light = new UiPalette(
            Rgb(0x252525), Rgb(0x545454), Rgb(0xf5f5f5), Rgb(0xffffff), Rgb(0xe5e5e5),
            Rgb(0x000000, 0.11f), Rgb(0x000000, 0.24f), Rgb(0x000000, 0.05f),
            Rgb(0x252525), Rgb(0xf5f5f5), Rgb(0xf1ec58), Rgb(0x252525),
            Rgb(0x000000, 0.45f), Rgb(0x202326), Rgb(0xebedef), Rgb(0xa72d32));
        public static readonly UiPalette Dark = new UiPalette(
            Rgb(0xebedef), Rgb(0xb3b8bc), Rgb(0x272a2e), Rgb(0x303438), Rgb(0x202326),
            Rgb(0xffffff, 0.13f), Rgb(0xffffff, 0.27f), Rgb(0xffffff, 0.06f),
            Rgb(0xe6e9eb), Rgb(0x191c1e), Rgb(0xf1ec58), Rgb(0x485058),
            Rgb(0x000000, 0.58f), Rgb(0x111315), Rgb(0xebedef), Rgb(0xff989d));

        private UiPalette(Color ink, Color muted, Color surface, Color raised, Color sunken,
            Color line, Color strongLine, Color hover, Color selected, Color onSelected,
            Color signal, Color switchOn, Color scrim, Color footer, Color footerInk, Color error)
        {
            Ink = ink; Muted = muted; Surface = surface; Raised = raised; Sunken = sunken;
            Line = line; StrongLine = strongLine; Hover = hover; Selected = selected;
            OnSelected = onSelected; Signal = signal; SwitchOn = switchOn; Scrim = scrim;
            Footer = footer; FooterInk = footerInk; Error = error;
        }

        public static UiPalette Blend(float dark) => new UiPalette(
            Color.Lerp(Light.Ink, Dark.Ink, dark), Color.Lerp(Light.Muted, Dark.Muted, dark),
            Color.Lerp(Light.Surface, Dark.Surface, dark), Color.Lerp(Light.Raised, Dark.Raised, dark),
            Color.Lerp(Light.Sunken, Dark.Sunken, dark), Color.Lerp(Light.Line, Dark.Line, dark),
            Color.Lerp(Light.StrongLine, Dark.StrongLine, dark), Color.Lerp(Light.Hover, Dark.Hover, dark),
            Color.Lerp(Light.Selected, Dark.Selected, dark), Color.Lerp(Light.OnSelected, Dark.OnSelected, dark),
            Color.Lerp(Light.Signal, Dark.Signal, dark), Color.Lerp(Light.SwitchOn, Dark.SwitchOn, dark),
            Color.Lerp(Light.Scrim, Dark.Scrim, dark), Color.Lerp(Light.Footer, Dark.Footer, dark),
            Color.Lerp(Light.FooterInk, Dark.FooterInk, dark), Color.Lerp(Light.Error, Dark.Error, dark));

        private static Color Rgb(int rgb, float alpha = 1f) => new Color(
            ((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, alpha);
    }
}
