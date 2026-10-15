using System;
using System.IO;
using System.Reflection;
using Verse;

namespace Oxygendsl.Mod
{
    [StaticConstructorOnStartup]
    internal static class OxygenRhahBridge
    {
        static OxygenRhahBridge()
        {
            // 等所有模组程序集和启动策略注册完成再加载反向依赖
            LongEventHandler.ExecuteWhenFinished(Activate);
        }

        static void Activate()
        {
            ModContentPack framework = null;
            bool rhahActive = false;
            foreach (ModContentPack mod in LoadedModManager.RunningModsListForReading)
            {
                if (string.Equals(mod.PackageIdPlayerFacing, "nanaloveyuki.oxygendsl", StringComparison.OrdinalIgnoreCase))
                    framework = mod;
                else if (string.Equals(mod.PackageIdPlayerFacing, "nanaloveyuki.ratkin.hungerandhavoc", StringComparison.OrdinalIgnoreCase))
                    rhahActive = true;
            }
            if (!rhahActive) return;

            try
            {
                if (framework == null) throw new InvalidOperationException("ODF content pack is unavailable.");
                Assembly api = null;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (assembly.GetName().Name != "HungerAndHavoc.Api") continue;
                    api = assembly;
                    break;
                }
                if (api == null || api.GetType("HungerAndHavoc.Api.IRHAH_PawnBehavior") == null)
                    throw new InvalidOperationException("The enabled RHAH mod did not load its behavior API.");

                string path = Path.Combine(framework.RootDir, "Rhah", "Assemblies", "Oxygendsl.Rhah.dll");
                Assembly adapter = Assembly.LoadFrom(path);
                Type type = adapter.GetType("Oxygendsl.Rhah.RhahBehaviorAdapter", true);
                MethodInfo activate = type.GetMethod("Activate", BindingFlags.Public | BindingFlags.Static,
                    null, Type.EmptyTypes, null);
                if (activate == null) throw new MissingMethodException(type.FullName, "Activate");
                activate.Invoke(null, null);
                Log.Message("[Oxygendsl] RHAH behavior adapter registered.");
            }
            catch (Exception exception)
            {
                Log.Error("[Oxygendsl] RHAH behavior adapter failed to initialize: " + exception);
            }
        }
    }
}
