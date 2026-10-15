using System;
using Verse;

namespace HungerAndHavoc.Guard
{
    [StaticConstructorOnStartup]
    internal static class OxygenDependencyGuard
    {
        static OxygenDependencyGuard()
        {
            for (int i = 0; i < LoadedModManager.RunningModsListForReading.Count; i++)
            {
                ModContentPack mod = LoadedModManager.RunningModsListForReading[i];
                if (!string.Equals(mod.PackageIdPlayerFacing, "nanaloveyuki.oxygendsl", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                bool coreLoaded = false;
                bool hostLoaded = false;
                for (int j = 0; j < mod.assemblies.loadedAssemblies.Count; j++)
                {
                    string name = mod.assemblies.loadedAssemblies[j].GetName().Name;
                    coreLoaded |= name == "Oxygendsl.Core";
                    hostLoaded |= name == "Oxygendsl.Mod";
                }
                if (coreLoaded && hostLoaded)
                {
                    return;
                }
            }

            // 静态启动只排队一次 原版在主界面创建可关闭弹窗
            DelayedErrorWindowRequest.Add(
                "RHAH_Guard_OxygenRequiredBody".Translate(),
                "RHAH_Guard_OxygenRequiredTitle".Translate());
        }
    }
}
