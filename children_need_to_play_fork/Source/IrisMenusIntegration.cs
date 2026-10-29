using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace ToddlersPlayExpanded
{
    internal static class IrisMenusIntegration
    {
        private const string PackageId = "Nanaloveyuki.IrisMenus";
        private delegate void RegisterListing(Verse.Mod owner, string id, Func<string> title,
            Action<Listing_Standard> draw, Action? save);
        private delegate void DrawChoice(Listing_Standard list, string anchor, string label,
            int selected, string[] options, Action<int> assign, bool enabled);
        private static DrawChoice? drawChoice;
        private static Func<bool>? isNative;
        private static readonly string[] moodOptions = new string[4];
        private static readonly Action<int> assignMood = value => TPE_Mod.Settings.adultPlushieMoodMode = (AdultPlushieMoodMode)value;
        private static object? optionsLanguage;

        internal static void Register(TPE_Mod owner)
        {
            ModContentPack? provider = null;
            foreach (ModContentPack content in LoadedModManager.RunningModsListForReading)
            {
                if (!string.Equals(content.PackageId, PackageId, StringComparison.OrdinalIgnoreCase)) continue;
                provider = content;
                break;
            }
            if (provider == null) return;

            try
            {
                Type? registry = null;
                foreach (Assembly assembly in provider.assemblies.loadedAssemblies)
                {
                    registry = assembly.GetType("IrisMenus.MenuRegistry", false);
                    if (registry != null) break;
                }
                MethodInfo? registerMethod = registry?.GetMethod("RegisterSubItemListing", new[]
                {
                    typeof(Verse.Mod), typeof(string), typeof(Func<string>), typeof(Action<Listing_Standard>), typeof(Action)
                });
                if (registerMethod == null)
                {
                    Log.Warning("[TPE] IrisMenus listing API is unavailable; standalone settings remain available.");
                    return;
                }
                var register = (RegisterListing)registerMethod.CreateDelegate(typeof(RegisterListing));
                Type? presentation = registry!.Assembly.GetType("IrisMenus.MenuPresentation", false);
                MethodInfo? listingFactory = presentation?.GetMethod("Listing", new[] { typeof(Action<Listing_Standard>) });
                MethodInfo? annotate = presentation == null ? null : registry.GetMethod("RegisterPresentation",
                    new[] { typeof(Verse.Mod), typeof(string), presentation });
                Type? controls = registry.Assembly.GetType("IrisMenus.MenuItemControls", false);
                MethodInfo? choiceMethod = controls?.GetMethod("ChoiceSort", new[]
                {
                    typeof(Listing_Standard), typeof(string), typeof(string), typeof(int),
                    typeof(string[]), typeof(Action<int>), typeof(bool)
                });
                MethodInfo? nativeGetter = controls?.GetProperty("IsNative")?.GetGetMethod();
                if (choiceMethod != null && nativeGetter != null)
                {
                    drawChoice = (DrawChoice)choiceMethod.CreateDelegate(typeof(DrawChoice));
                    isNative = (Func<bool>)nativeGetter.CreateDelegate(typeof(Func<bool>));
                }
                for (int page = 0; page < TPE_Mod.PageIds.Length; page++)
                {
                    int category = page;
                    string id = TPE_Mod.PageIds[page];
                    Action<Listing_Standard> draw = list => owner.DrawSettingsPage(category, list);
                    register(owner, id, () => TPE_Mod.PageTitle(category), draw, owner.WriteSettings);
                    if (listingFactory != null && annotate != null)
                        annotate.Invoke(null, new[] { (object)owner, id, listingFactory.Invoke(null, new object[] { draw })! });
                }
            }
            catch (Exception exception)
            {
                Log.Error("[TPE] Optional IrisMenus registration failed; standalone settings remain available. " + exception);
            }
        }

        internal static bool DrawAdultMoodChoice(Listing_Standard list)
        {
            if (drawChoice == null || isNative == null || !isNative()) return false;
            if (!ReferenceEquals(optionsLanguage, LanguageDatabase.activeLanguage))
            {
                for (int i = 0; i < moodOptions.Length; i++)
                    moodOptions[i] = ("TPE_Settings_AdultMoodMode_" + (AdultPlushieMoodMode)i).Translate();
                optionsLanguage = LanguageDatabase.activeLanguage;
            }
            drawChoice(list, "adult-plush-mood", "TPE_Settings_AdultMoodMode_Label".Translate(),
                (int)TPE_Mod.Settings.adultPlushieMoodMode, moodOptions, assignMood, true);
            return true;
        }
    }
}
