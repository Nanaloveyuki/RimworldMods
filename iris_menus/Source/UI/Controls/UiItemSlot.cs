using System;
using UnityEngine;
using Verse;

namespace IrisMenus.UI
{
    /// <summary>A cached single-unit presentation. Recreate after language or item value changes.</summary>
    public sealed class UiThingEntry
    {
        public UiThingEntry(ThingDef def, string key = null, string label = null, string description = null, string tooltip = null)
            : this(def, null, key, label, description, tooltip) { }

        public UiThingEntry(Thing thing, string key = null, string label = null, string description = null, string tooltip = null)
            : this(thing?.def, thing, key, label, description, tooltip) { }

        private UiThingEntry(ThingDef def, Thing thing, string key, string label, string description, string tooltip)
        {
            Def = def ?? throw new ArgumentNullException(nameof(def));
            Thing = thing;
            Key = key ?? (thing == null ? def.defName : thing.ThingID);
            if (string.IsNullOrEmpty(Key)) throw new ArgumentException("An item key is required.", nameof(key));
            Label = label ?? (thing == null ? def.LabelCap.ToString() : thing.LabelCapNoCount.ToString());
            Description = description ?? string.Empty;
            MarketValue = thing == null ? def.BaseMarketValue : thing.MarketValue;
            ValueText = "IrisMenus_ItemValue_Label".Translate(MarketValue.ToString("0.##")).ToString();
            ExtraTooltip = tooltip ?? string.Empty;
            LegacyTooltip = Label + "\n" + Description + "\n" + ValueText + "\n" + ExtraTooltip;
        }

        public string Key { get; }
        public ThingDef Def { get; }
        public Thing Thing { get; }
        public string Label { get; }
        public string Description { get; }
        public float MarketValue { get; }
        public string ValueText { get; }
        public string ExtraTooltip { get; }
        public string LegacyTooltip { get; }
    }

    /// <summary>Endfield item face: icon, bottom value fade, band markers and selected corner brackets.</summary>
    public sealed class UiItemSlot
    {
        private readonly UiContext context;
        private readonly UiHoverCard tooltip;
        private UiControlVisual visual;
        private UiTween selection;
        private UiThingEntry measuredEntry;
        private string shortLabel, shortDescription, tip;
        private float measuredWidth = -1f, measuredFont;
        private float measuredScale;
        private FontStyle measuredStyle;
        private int tooltipBand = -1;
        private bool initialized;

        public UiItemSlot(UiContext context)
        {
            this.context = context ?? throw new ArgumentNullException(nameof(context));
            Id = context.CreateId();
            tooltip = new UiHoverCard(context);
        }

        public UiId Id { get; }
        public bool HasTooltip => tooltip.IsVisible;

        public bool Draw(Rect rect, UiThingEntry entry, bool selected, bool enabled = true, bool row = false, float scale = 1f, bool readOnly = false)
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            if (float.IsNaN(scale) || float.IsInfinity(scale) || scale <= 0f) throw new ArgumentOutOfRangeException(nameof(scale));
            UiInteraction interaction = context.Interact(Id, rect, enabled && !readOnly);
            if (interaction.Clicked) selected = !selected;
            visual.Step(context, interaction, enabled);
            if (!initialized) { selection.Snap(selected ? 1f : 0f); initialized = true; }
            float amount = selection.Step(selected ? 1f : 0f, context.Time, UiMotion.Standard, context.ReducedMotion);
            UiPalette palette = context.Palette;
            float iconSize = row ? Mathf.Min(rect.height - 8f * scale, 48f * scale) : Mathf.Min(rect.width, rect.height);
            Rect face = row ? new Rect(rect.x + 8f * scale, rect.center.y - iconSize * 0.5f, iconSize, iconSize) : rect;
            int band = ItemValueColors.BandFor(entry.MarketValue);
            if (!ReferenceEquals(measuredEntry, entry) || measuredWidth != rect.width || measuredFont != context.FontSize || measuredStyle != context.FontStyle || measuredScale != scale)
            {
                measuredEntry = entry;
                measuredWidth = rect.width;
                measuredFont = context.FontSize;
                measuredStyle = context.FontStyle;
                measuredScale = scale;
                float labelWidth = Mathf.Max(0f, rect.width - iconSize - 180f * scale);
                shortLabel = UiTextMetrics.Ellipsize(context, entry.Label, labelWidth);
                shortDescription = UiTextMetrics.Ellipsize(context, entry.Description, labelWidth);
                tooltipBand = -1;
            }
            if (tooltipBand != band)
            {
                tooltipBand = band;
                tip = "IrisMenus_ItemValue_Tooltip".Translate(entry.Label, entry.MarketValue.ToString("0.##"), band + 1).ToString();
                if (!string.IsNullOrEmpty(entry.Description)) tip += "\n" + entry.Description;
                if (!string.IsNullOrEmpty(entry.ExtraTooltip)) tip += "\n" + entry.ExtraTooltip;
            }
            using (context.Opacity(visual.Opacity))
            {
                if (row)
                {
                    context.Fill(rect, Color.Lerp(visual.Background(palette, palette.Raised), palette.Sunken, amount));
                    Color stripe = palette.Signal; stripe.a *= amount;
                    context.Fill(new Rect(rect.x, rect.y, 3f * scale, rect.height), stripe);
                    context.Fill(new Rect(rect.x, rect.yMax - scale, rect.width, scale), palette.Line);
                }
                context.Fill(face, palette.Raised);
                Color valueColor = ItemValueColors.ColorFor(entry.MarketValue);
                for (int i = 0; i < 8; i++)
                {
                    Color fade = valueColor; fade.a *= 0.5f * (i + 1) / 8f;
                    context.Fill(new Rect(face.x, face.yMax - face.height * 0.4f + i * face.height * 0.05f, face.width, face.height * 0.05f + 0.25f), fade);
                }
                context.Fill(new Rect(face.x, face.yMax - 3f * scale, face.width, 3f * scale), valueColor);
                context.Border(face, Color.Lerp(palette.Line, palette.StrongLine, visual.Hover));
                if (context.IsRepaint)
                {
                    Color saved = GUI.color;
                    GUI.color = new Color(1f, 1f, 1f, context.Alpha);
                    try
                    {
                        Rect icon = face.ContractedBy(7f * scale);
                        if (entry.Thing == null) Widgets.ThingIcon(icon, entry.Def);
                        else Widgets.ThingIcon(icon, entry.Thing);
                    }
                    finally { GUI.color = saved; }
                }
                int markers = Mathf.Min(Mathf.Max(0, band) + 1, Mathf.Max(0, Mathf.FloorToInt((face.height - 12f * scale) / (6f * scale))));
                for (int i = 0; i < markers; i++)
                {
                    float y = face.center.y + (i - (markers - 1) * 0.5f) * 6f * scale;
                    float x = face.x + 3f * scale;
                    context.Fill(new Rect(x, y, 2f * scale, 2f * scale), palette.Muted);
                }
                if (amount > 0f)
                {
                    Color edge = palette.Ink; edge.a *= amount;
                    float corner = 8f * scale;
                    for (int i = 0; i < 4; i++)
                    {
                        float x = i % 2 == 0 ? face.x - 2f * scale : face.xMax + 2f * scale;
                        float y = i < 2 ? face.y - 2f * scale : face.yMax + 2f * scale;
                        context.Fill(new Rect(i % 2 == 0 ? x : x - corner, y, corner, 2f * scale), edge);
                        context.Fill(new Rect(x, i < 2 ? y : y - corner, 2f * scale, corner), edge);
                    }
                }
                if (row)
                {
                    float x = face.xMax + 12f * scale;
                    Rect label = new Rect(x, rect.y, Mathf.Max(0f, rect.width - iconSize - 180f * scale), rect.height);
                    if (string.IsNullOrEmpty(entry.Description)) context.Label(label, shortLabel, palette.Ink);
                    else
                    {
                        context.Label(new Rect(label.x, label.y + 5f * scale, label.width, label.height * 0.5f), shortLabel, palette.Ink);
                        float font = context.FontSize;
                        context.FontSize = font * 0.8f;
                        context.Label(new Rect(label.x, label.center.y, label.width, label.height * 0.5f - 3f * scale), shortDescription, palette.Muted);
                        context.FontSize = font;
                    }
                    context.Label(new Rect(rect.xMax - 152f * scale, rect.y, 118f * scale, rect.height), entry.ValueText, palette.Muted, TextAnchor.MiddleRight);
                    Rect check = new Rect(rect.xMax - 24f * scale, rect.center.y - 8f * scale, 16f * scale, 16f * scale);
                    if (!readOnly) context.Border(check, palette.StrongLine);
                    if (selected && !readOnly)
                    {
                        context.Line(new Vector2(check.x + 3f * scale, check.center.y), new Vector2(check.center.x - scale, check.yMax - 4f * scale), palette.Ink, 2f * scale);
                        context.Line(new Vector2(check.center.x - scale, check.yMax - 4f * scale), new Vector2(check.xMax - 3f * scale, check.y + 4f * scale), palette.Ink, 2f * scale);
                    }
                }
                visual.DrawFocus(context, rect);
            }
            tooltip.Observe(rect, tip, Id);
            return selected;
        }

        public void DrawTooltip(Rect bounds, float scale = 1f)
        {
            tooltip.DrawOverlay(bounds, scale);
        }
    }
}
