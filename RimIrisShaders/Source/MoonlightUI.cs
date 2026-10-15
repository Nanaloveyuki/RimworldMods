using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using RimIris;
using UnityEngine;
using Verse;

namespace RimIrisShaders
{
    [StaticConstructorOnStartup]
    public static class MoonlightUI
    {
        private const string PackId = "rimirisshaders.moonlight";

        static MoonlightUI()
        {
            try
            {
                // Startup constructors have no guaranteed order across mods.
                RuntimeHelpers.RunClassConstructor(typeof(RimIrisInitializer).TypeHandle);
                PackEntry entry = PackRegistry.Get(PackId);
                if (entry == null) throw new InvalidOperationException("Moonlight pack was not registered.");
                var mod = LoadedModManager.RunningMods.First(m =>
                    string.Equals(m.PackageId, "RimIrisShaders.Moonlight", StringComparison.OrdinalIgnoreCase));
                var json = (Dictionary<string, object>)MiniJson.Parse(File.ReadAllText(
                    Path.Combine(mod.RootDir, "ShaderPacks", "Moonlight", "options.json")));
                var options = new List<PackOptionDef>();
                foreach (Dictionary<string, object> option in (List<object>)json["options"])
                {
                    options.Add(new PackOptionDef {
                        Id = (string)option["id"],
                        Min = Convert.ToSingle(option["min"]),
                        Max = Convert.ToSingle(option["max"]),
                        Default = Convert.ToSingle(option["default"])
                    });
                }
                entry.NameKey = "Moonlight_Name";
                entry.DescKey = "Moonlight_Description";
                entry.DrawOptions = list => DrawOptions(list, entry, options);
            }
            catch (Exception ex)
            {
                Log.Error("[Moonlight] UI localization failed: " + ex);
            }
        }

        private static void DrawOptions(Listing_Standard list, PackEntry entry, List<PackOptionDef> options)
        {
            list.Label(entry.DisplayName);
            list.Label(entry.DisplayDesc);
            list.Gap();
            var settings = RimIrisBetaMod.Settings;
            if (settings == null || entry.Status != PackStatus.Ok ||
                (settings.activePack != PackId && (settings.enabledAddons == null || !settings.enabledAddons.Contains(PackId))))
            {
                list.Label("Moonlight_Disabled".Translate());
                return;
            }
            foreach (var option in options)
            {
                float value;
                if (!settings.packOptionValues.TryGetValue(PackId + "|" + option.Id, out value))
                    value = option.Default;
                list.Label(("Moonlight_" + option.Id).Translate() + ": " + value.ToString("0.##"));
                float next = list.Slider(value, option.Min, option.Max);
                if (!Mathf.Approximately(value, next)) PackEngine.SetOptionValue(PackId, option, next);
            }
        }
    }
}
