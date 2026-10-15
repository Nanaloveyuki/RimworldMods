using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Xml;
using RimIris;
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
                // NameKey/DescKey are read on every draw. Bundle option panes are owned by
                // PackEngine; replacing DrawOptions hides them and can fail the addon.
                entry.NameKey = "Moonlight_Name";
                entry.DescKey = "Moonlight_Description";
                ApplyOptionLabels();
            }
            catch (Exception ex)
            {
                Log.Error("[Moonlight] UI localization failed: " + ex);
            }
        }

        private static void ApplyOptionLabels()
        {
            List<PackOptionDef> options = PackEngine.GetOptionDefs(PackId);
            if (options == null) return;
            LoadedLanguage language = LanguageDatabase.activeLanguage;
            string folder = language == null ? "English" : language.folderName;
            Dictionary<string, string> labels = ReadLabels(folder) ?? new Dictionary<string, string>();
            Dictionary<string, string> english = folder == "English" ? labels : ReadLabels("English");
            foreach (PackOptionDef option in options)
            {
                string label;
                if (labels.TryGetValue("Moonlight_" + option.Id, out label) ||
                    (english != null && english.TryGetValue("Moonlight_" + option.Id, out label)))
                    option.Label = label;
            }
        }

        private static Dictionary<string, string> ReadLabels(string folder)
        {
            ModContentPack mod = LoadedModManager.RunningMods.FirstOrDefault(m =>
                string.Equals(m.PackageId, "RimIrisShaders.Moonlight", StringComparison.OrdinalIgnoreCase));
            if (mod == null) return null;
            string path = Path.Combine(mod.RootDir, "Languages", folder, "Keyed", "Moonlight.xml");
            if (!File.Exists(path)) return null;
            var doc = new XmlDocument();
            doc.Load(path);
            XmlNode root = doc.DocumentElement;
            if (root == null) return null;
            var labels = new Dictionary<string, string>();
            foreach (XmlNode node in root.ChildNodes)
            {
                if (node.NodeType != XmlNodeType.Element || string.IsNullOrEmpty(node.InnerText)) continue;
                labels[node.Name] = node.InnerText;
            }
            return labels;
        }
    }
}
