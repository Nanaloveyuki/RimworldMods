using System;
using System.Reflection;
using UnityEngine;
using Verse;

namespace PrisonersPayToEat2
{
    internal static class IrisMenusBridge
    {
        private const string PackageId = "Nanaloveyuki.IrisMenus";
        private static bool initialized;

        private delegate void RegisterSubItem(Mod owner, string pageId, Func<string> title,
            Action<Rect> draw, Action save);

        internal static void Register(PrisonersPayToEat2Mod owner)
        {
            if (initialized) return;
            initialized = true;

            ModContentPack iris = null;
            foreach (ModContentPack content in LoadedModManager.RunningModsListForReading)
            {
                if (!string.Equals(content.PackageId, PackageId, StringComparison.OrdinalIgnoreCase)) continue;
                iris = content;
                break;
            }
            if (iris == null) return;

            try
            {
                Type registry = null;
                var assemblies = iris.assemblies?.loadedAssemblies;
                if (assemblies != null)
                {
                    foreach (Assembly assembly in assemblies)
                    {
                        registry = assembly?.GetType("IrisMenus.MenuRegistry", false);
                        if (registry != null) break;
                    }
                }

                MethodInfo method = registry?.GetMethod("RegisterSubItem", BindingFlags.Public | BindingFlags.Static,
                    null, new[] { typeof(Mod), typeof(string), typeof(Func<string>), typeof(Action<Rect>), typeof(Action) }, null);
                if (method == null || method.ReturnType != typeof(void))
                {
                    Log.Warning("[PPTE2] IrisMenus is enabled but its RegisterSubItem API was not found; the full vanilla settings remain available.");
                    return;
                }

                var register = (RegisterSubItem)method.CreateDelegate(typeof(RegisterSubItem));
                RegisterPage(register, owner, 0, "general", "PPTE2_TabGeneral");
                RegisterPage(register, owner, 1, "work-wages", "PPTE2_TabWorkWages");
                RegisterPage(register, owner, 2, "food-prices", "PPTE2_TabFoodPrices");
                RegisterPage(register, owner, 3, "social", "PPTE2_TabSocial");
                RegisterPage(register, owner, 4, "prison-work", "PPTE2_TabPrisonWork");
                RegisterPage(register, owner, 5, "requests", "PPTE2_TabRequest");
                RegisterPage(register, owner, 6, "joy-hygiene", "PPTE2_TabJoyHygiene");
            }
            catch (Exception exception)
            {
                Log.Error("[PPTE2] IrisMenus settings registration failed: " + exception);
            }
        }

        private static void RegisterPage(RegisterSubItem register, Mod owner, int page, string id, string titleKey)
        {
            // Titles stay language-aware; native pages own their full rect without the vanilla tab strip.
            register(owner, id, () => titleKey.Translate(),
                rect => PrisonersPayToEat2Mod.Settings.DrawSettingsPage(page, rect), null);
        }
    }
}
