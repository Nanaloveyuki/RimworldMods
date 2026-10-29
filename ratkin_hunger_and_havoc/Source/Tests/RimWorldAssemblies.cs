using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;

namespace HungerAndHavoc.Tests
{
    internal static class RimWorldAssemblies
    {
        static int registered;

        [ModuleInitializer]
        internal static void Init()
        {
            EnsureResolved();
            InitializeGameConfiguration();
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        static void InitializeGameConfiguration()
        {
            RuntimeHelpers.RunClassConstructor(typeof(RHAH_ContentApplierTests).TypeHandle);
        }

        internal static void EnsureResolved()
        {
            if (Interlocked.Exchange(ref registered, 1) == 1)
            {
                return;
            }

            AppDomain.CurrentDomain.AssemblyResolve += Resolve;
        }

        static Assembly Resolve(object sender, ResolveEventArgs args)
        {
            string simpleName = new AssemblyName(args.Name).Name;
            if (string.IsNullOrEmpty(simpleName))
            {
                return null;
            }

            foreach (string managed in ManagedDirs())
            {
                string path = Path.Combine(managed, simpleName + ".dll");
                if (File.Exists(path))
                {
                    return Assembly.LoadFrom(path);
                }
            }

            return null;
        }

        static IEnumerable<string> ManagedDirs()
        {
            string env = Environment.GetEnvironmentVariable("RIMWORLD_DIR");
            if (!string.IsNullOrEmpty(env))
            {
#if RIMWORLD_1_7
                yield return env;
#else
                yield return Path.Combine(env, "RimWorldWin64_Data", "Managed");
#endif
            }

#if RIMWORLD_1_7
            yield return "/mnt/e/Apps/Steam/steamapps/common/RimWorld";
            yield return @"E:\Apps\Steam\steamapps\common\RimWorld";
#else
            yield return "/mnt/e/Apps/Steam/steamapps/common/RimWorld/RimWorldWin64_Data/Managed";
            yield return @"E:\Apps\Steam\steamapps\common\RimWorld\RimWorldWin64_Data\Managed";
#endif
            yield return AppDomain.CurrentDomain.BaseDirectory;
        }
    }
}
