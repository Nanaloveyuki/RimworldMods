using System.IO;
using UnityEngine;
using Oxygendsl.Mod;
using Verse;

namespace Odf.PrisonerNames
{
    public sealed class PrisonerNamesMod : Mod
    {
        public PrisonerNamesMod(ModContentPack content) : base(content)
        {
            PrisonerNamesHost.Register();
            NamingRules.ModuleCatalog = new GameModuleCatalog();
            NamingRules.InstalledDefault = LoadInstalledDefault;
            NamingRules.InstalledEvents = () => LoadInstalled("events.oxy");
            NamePatches.Install();
            LongEventHandler.ExecuteWhenFinished(PrisonerNamesHost.Subscribe);
            PrisonerSettingsWindow.Register(this);
        }

        public override string SettingsCategory()
        {
            return "OPN_Title".Translate();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            IrisMenus.MenuRegistry.Open();
        }

        static string LoadInstalledDefault(bool chinese)
        {
            return LoadInstalled(chinese ? "default-zh.oxy" : "default-en.oxy");
        }

        static string LoadInstalled(string file)
        {
            PrisonerNamesMod mod = LoadedModManager.GetMod<PrisonerNamesMod>();
            if (mod == null || mod.Content == null) return null;
            foreach (string folder in mod.Content.foldersToLoadDescendingOrder)
            {
                string path = Path.Combine(folder, "Oxygen", file);
                if (File.Exists(path)) return File.ReadAllText(path);
            }
            return null;
        }
    }

    internal static class NamingLanguage
    {
        internal static bool Chinese
        {
            get { return LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.LegacyFolderName == "ChineseSimplified"; }
        }
    }

    public static class PrisonerNamesApi
    {
        public static GameComponent_PrisonerNames Runtime
        {
            get { return GameComponent_PrisonerNames.Current; }
        }

        public static void OpenWindow()
        {
            if (Current.ProgramState != ProgramState.Playing || Runtime == null) return;
            if (!Find.WindowStack.IsOpen<NamingWindow>()) Find.WindowStack.Add(new NamingWindow(Runtime));
        }
    }
}
