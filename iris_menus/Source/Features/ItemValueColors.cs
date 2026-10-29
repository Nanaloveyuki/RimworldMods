using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace IrisMenus
{
    public sealed class ItemValueBand : IExposable
    {
        private float minimum;
        private float red, green, blue;

        public ItemValueBand() { }
        public ItemValueBand(float minimum, Color color)
        {
            this.minimum = minimum;
            Color = color;
        }

        public float Minimum { get => minimum; internal set => minimum = value; }
        public Color Color
        {
            get => new Color(red, green, blue, 1f);
            internal set { red = value.r; green = value.g; blue = value.b; }
        }

        public void ExposeData()
        {
            Scribe_Values.Look(ref minimum, "minimum", 0f);
            Scribe_Values.Look(ref red, "red", 0.6f);
            Scribe_Values.Look(ref green, "green", 0.6f);
            Scribe_Values.Look(ref blue, "blue", 0.6f);
        }
    }

    /// <summary>Contiguous, lower-inclusive single-item market-value bands shared by item renderers.</summary>
    public static class ItemValueColors
    {
        private static readonly List<ItemValueBand> fallback = CreateDefaults();
        private static List<ItemValueBand> Current => IrisMenusMod.Instance?.Settings.ItemValueBands ?? fallback;

        public static Color ColorFor(float marketValue)
        {
            List<ItemValueBand> bands = Current;
            return bands[FindBand(bands, marketValue)].Color;
        }

        public static int BandFor(float marketValue) => FindBand(Current, marketValue);

        private static int FindBand(List<ItemValueBand> bands, float value)
        {
            if (float.IsNaN(value) || value <= 0f) return 0;
            int low = 0, high = bands.Count - 1;
            while (low < high)
            {
                int middle = low + (high - low + 1) / 2;
                if (value >= bands[middle].Minimum) low = middle;
                else high = middle - 1;
            }
            return low;
        }

        internal static List<ItemValueBand> CreateDefaults() => new List<ItemValueBand>
        {
            new ItemValueBand(0f, new Color(0.60f, 0.62f, 0.65f)),
            new ItemValueBand(50f, new Color(0.29f, 0.72f, 0.43f)),
            new ItemValueBand(200f, new Color(0.25f, 0.56f, 0.90f)),
            new ItemValueBand(500f, new Color(0.66f, 0.40f, 0.86f)),
            new ItemValueBand(1500f, new Color(0.91f, 0.69f, 0.24f))
        };

        internal static void Normalize(ref List<ItemValueBand> bands)
        {
            if (bands == null || bands.Count == 0) { bands = CreateDefaults(); return; }
            bands.RemoveAll(band => band == null || !Finite(band.Minimum) || band.Minimum < 0f);
            bands.Sort((left, right) => left.Minimum.CompareTo(right.Minimum));
            for (int i = bands.Count - 1; i > 0; i--)
                if (bands[i].Minimum == bands[i - 1].Minimum) bands.RemoveAt(i);
            if (bands.Count == 0 || bands[0].Minimum != 0f)
                bands.Insert(0, new ItemValueBand(0f, fallback[0].Color));
            for (int i = 0; i < bands.Count; i++) bands[i].Color = Opaque(bands[i].Color);
        }

        internal static Color Opaque(Color color) => new Color(Channel(color.r), Channel(color.g), Channel(color.b), 1f);
        private static float Channel(float value) => Finite(value) ? Mathf.Clamp01(value) : 0.6f;
        internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

        internal static bool SetMinimum(List<ItemValueBand> bands, int index, float value)
        {
            if (index <= 0 || index >= bands.Count || !Finite(value) || value <= bands[index - 1].Minimum
                || (index + 1 < bands.Count && value >= bands[index + 1].Minimum)) return false;
            bands[index].Minimum = value;
            return true;
        }

        internal static bool Add(List<ItemValueBand> bands)
        {
            ItemValueBand last = bands[bands.Count - 1];
            float above = (float)Math.Min(float.MaxValue, Math.Max((double)last.Minimum * 2d, (double)last.Minimum + 50d));
            if (above > last.Minimum)
            {
                bands.Add(new ItemValueBand(above, last.Color));
                return true;
            }
            // At the finite limit, split the highest range that still has a representable midpoint.
            for (int i = bands.Count - 1; i > 0; i--)
            {
                float middle = (float)(((double)bands[i - 1].Minimum + bands[i].Minimum) * 0.5d);
                if (middle <= bands[i - 1].Minimum || middle >= bands[i].Minimum) continue;
                bands.Insert(i, new ItemValueBand(middle, bands[i - 1].Color));
                return true;
            }
            return false;
        }
    }
}
