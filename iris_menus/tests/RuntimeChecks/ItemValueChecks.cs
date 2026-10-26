using System;
using System.Collections.Generic;
using IrisMenus;
using UnityEngine;

internal static class ItemValueChecks
{
    internal static void Run(Action<bool, string> check)
    {
        MenuSettings settings = IrisMenusMod.Instance.Settings;
        var saved = settings.ItemValueBands;
        try
        {
            settings.ItemValueBands = ItemValueColors.CreateDefaults();
            float[] values = { -1f, 0f, 49.99f, 50f, 199.99f, 200f, 499.99f, 500f, 1499.99f, 1500f, float.MaxValue };
            int[] bands = { 0, 0, 0, 1, 1, 2, 2, 3, 3, 4, 4 };
            for (int i = 0; i < values.Length; i++) check(ItemValueColors.BandFor(values[i]) == bands[i], "Value threshold is lower-inclusive: " + values[i]);
            check(!ItemValueColors.SetMinimum(settings.ItemValueBands, 0, 10f), "Base zero band cannot be moved");
            check(!ItemValueColors.SetMinimum(settings.ItemValueBands, 1, 200f), "Adjacent thresholds cannot overlap");
            check(ItemValueColors.SetMinimum(settings.ItemValueBands, 1, 100f) && ItemValueColors.BandFor(75f) == 0 && ItemValueColors.BandFor(100f) == 1, "Editing threshold immediately changes classification");
            settings.ItemValueBands[1].Color = new Color(0.2f, 0.3f, 0.4f);
            Color applied = ItemValueColors.ColorFor(100f);
            check(applied.r == 0.2f && applied.g == 0.3f && applied.b == 0.4f && applied.a == 1f, "Applied color changes lookup without a renderer cache reset");
            check(ItemValueColors.Add(settings.ItemValueBands) && ItemValueColors.BandFor(3000f) == 5, "Adding a band preserves lower ranges and splits highest range");
            settings.ItemValueBands.RemoveAt(1);
            check(ItemValueColors.BandFor(100f) == 0 && ItemValueColors.BandFor(200f) == 1, "Deleting middle band leaves contiguous ranges");
            var malformed = new List<ItemValueBand>
            {
                null, new ItemValueBand(float.NaN, new Color()), new ItemValueBand(-1f, new Color()),
                new ItemValueBand(200f, new Color(2f, -1f, float.NaN)), new ItemValueBand(200f, new Color(2f, -1f, float.NaN))
            };
            ItemValueColors.Normalize(ref malformed);
            check(malformed.Count == 2 && malformed[0].Minimum == 0f && malformed[1].Minimum == 200f, "Loading repairs missing base, invalid and duplicate thresholds");
            Color repaired = malformed[1].Color;
            check(repaired.r == 1f && repaired.g == 0f && repaired.b == 0.6f && repaired.a == 1f, "Loading repairs invalid RGB channels");
            settings.ItemValueBands = new List<ItemValueBand>
            {
                new ItemValueBand(0f, Color.white), new ItemValueBand(float.MaxValue, Color.white)
            };
            check(ItemValueColors.Add(settings.ItemValueBands) && settings.ItemValueBands.Count == 3
                && settings.ItemValueBands[1].Minimum > 0f && settings.ItemValueBands[1].Minimum < float.MaxValue,
                "Adding at the finite limit splits a representable range without overflow");
            check(!ItemValueColors.SetMinimum(settings.ItemValueBands, 1, float.PositiveInfinity)
                && !ItemValueColors.SetMinimum(settings.ItemValueBands, 1, float.NaN), "Nonfinite thresholds are rejected");
            check(ItemValueColors.BandFor(float.NaN) == 0 && ItemValueColors.BandFor(float.PositiveInfinity) == 2,
                "Nonfinite market values preserve lookup bounds");
        }
        finally { settings.ItemValueBands = saved; }
    }
}
