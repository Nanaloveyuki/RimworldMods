using System;
using UnityEngine;

namespace IrisMenus.UI
{
    public enum UiButtonVariant { Control, Light, Action, Text, Ghost }

    public sealed class UiButton
    {
        private readonly UiContext context;
        private UiControlVisual visual;
        private UiTween marker;

        public UiButton(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
        }

        public UiId Id { get; }

        public bool Draw(Rect rect, string label, bool enabled = true, bool primary = false,
            UiButtonVariant? variant = null, UiGlyphKind icon = UiGlyphKind.None,
            float iconRotation = 0f, float hoverIconRotation = 0f, float scale = 1f)
        {
            UiInteraction interaction = context.Interact(Id, rect, enabled);
            DrawVisual(rect, label, interaction, enabled, primary, variant, icon, iconRotation, hoverIconRotation, scale);
            return interaction.Clicked;
        }

        internal Color DrawVisual(Rect rect, string label, UiInteraction interaction, bool enabled = true,
            bool primary = false, UiButtonVariant? variant = null, UiGlyphKind icon = UiGlyphKind.None,
            float iconRotation = 0f, float hoverIconRotation = 0f, float scale = 1f, bool drawContent = true)
        {
            visual.Step(context, interaction, enabled);
            bool iconOnly = string.IsNullOrEmpty(label) && icon != UiGlyphKind.None;
            UiButtonVariant kind = variant ?? (iconOnly ? UiButtonVariant.Ghost : primary ? UiButtonVariant.Control : UiButtonVariant.Light);
            bool marked = !iconOnly && (kind == UiButtonVariant.Control || kind == UiButtonVariant.Light);
            UiGlyphKind glyphKind = icon == UiGlyphKind.None && kind == UiButtonVariant.Text ? UiGlyphKind.ArrowRight : icon;
            float morph = marker.Step(enabled && context.IsInputAllowed && (interaction.Hovered || interaction.Pressed) ? 1f : 0f,
                context.Time, UiMotion.Enter, context.ReducedMotion);
            UiPalette palette = context.Palette;
            float unit = Mathf.Max(0f, scale);
            Color ink = palette.Ink;
            using (context.Opacity(visual.Opacity))
            {
                if (kind == UiButtonVariant.Control)
                {
                    Color background = Color.Lerp(new Color(56f / 255f, 56f / 255f, 56f / 255f), new Color(72f / 255f, 72f / 255f, 72f / 255f), visual.Hover);
                    context.Fill(rect, Color.Lerp(background, new Color(40f / 255f, 40f / 255f, 40f / 255f), visual.Press));
                    ink = Color.Lerp(new Color(238f / 255f, 238f / 255f, 238f / 255f), Color.white, visual.Hover);
                }
                else if (kind == UiButtonVariant.Light)
                {
                    Color background = Color.Lerp(palette.Button, palette.Marker, visual.Hover * 0.16f);
                    context.Fill(rect, Color.Lerp(background, palette.Ink, visual.Press * 0.1f));
                }
                else if (kind == UiButtonVariant.Action)
                {
                    context.Fill(rect, palette.Signal);
                    ink = UiPalette.Light.Ink;
                    context.Fill(new Rect(rect.xMax - 19f * unit, rect.y + rect.height * 0.175f, 3f * unit, rect.height * 0.65f),
                        Color.Lerp(new Color(0f, 0f, 0f, 0.2f), new Color(0f, 0f, 0f, 0.4f), visual.Hover));
                }
                else if (kind == UiButtonVariant.Ghost)
                {
                    Color background = palette.Selected;
                    background.a *= Mathf.Max(visual.Hover, visual.Press);
                    context.Fill(rect, background);
                    ink = Color.Lerp(palette.Ink, palette.OnSelected, Mathf.Max(visual.Hover, visual.Press));
                }
                else ink = Color.Lerp(ink, palette.Muted, visual.Press);

                if (!drawContent)
                {
                    visual.DrawFocus(context, rect);
                    return ink;
                }
                float padding = marked ? (rect.height <= 32f * unit ? 28f : 40f) * unit : 10f * unit;
                Rect content = new Rect(rect.x + padding, rect.y, Mathf.Max(0f, rect.width - padding * 2f), rect.height);
                if (kind == UiButtonVariant.Action) content.width = Mathf.Max(0f, content.width - 18f * unit);
                if (marked)
                {
                    bool small = rect.height <= 32f * unit;
                    float height = Mathf.Min(rect.height * 0.5f, (small ? 16f : 20f) * unit);
                    float bar = (small ? 2f : 3f) * unit;
                    float side = height * 0.6f;
                    float width = side * 0.8660254f;
                    float left = rect.x + ((small ? 12f : 16f) + (small ? 4f : 6f) * morph) * unit;
                    float top = rect.center.y - height * 0.5f;
                    // Matching vertex order keeps the bar continuous as the right edge collapses to a tip.
                    Vector2 a = new Vector2(left, top + height * 0.2f * morph);
                    Vector2 b = new Vector2(left + UiMotion.Lerp(bar, width, morph), top + height * 0.5f * morph);
                    Vector2 c = new Vector2(left + bar * (1f - morph), top + height * UiMotion.Lerp(1f, 0.8f, morph));
                    Vector2 d = new Vector2(left, c.y);
                    Color mark = kind == UiButtonVariant.Control ? palette.Signal : UiPalette.Light.Marker;
                    context.Triangle(a, b, c, mark);
                    context.Triangle(a, c, d, mark);
                }
                if (glyphKind != UiGlyphKind.None)
                {
                    float iconSize = Mathf.Min(16f * unit, Mathf.Min(rect.height, iconOnly ? rect.width : content.width));
                    bool iconAtEnd = kind == UiButtonVariant.Text && !iconOnly;
                    Rect glyph = new Rect(iconOnly ? rect.center.x - iconSize * 0.5f : iconAtEnd ? content.xMax - iconSize : content.x,
                        rect.center.y - iconSize * 0.5f, iconSize, iconSize);
                    UiGlyph.Draw(context, glyph, glyphKind, ink, iconRotation + hoverIconRotation * morph + (kind == UiButtonVariant.Text && icon == UiGlyphKind.None ? 45f : 0f));
                    if (!iconOnly)
                    {
                        if (iconAtEnd) content.xMax -= iconSize + 8f * unit;
                        else content.xMin += iconSize + 8f * unit;
                    }
                }
                if (!iconOnly) context.Label(content, label ?? string.Empty, ink, TextAnchor.MiddleCenter);
                if (kind == UiButtonVariant.Text)
                    context.Fill(new Rect(content.x, rect.yMax - 2f * unit, content.width, unit), ink);
                visual.DrawFocus(context, rect);
            }
            return ink;
        }
    }
}
