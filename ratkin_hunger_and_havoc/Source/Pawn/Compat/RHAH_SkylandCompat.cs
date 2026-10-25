using System;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal static class RHAH_SkylandCompat
    {
        static bool resolved;
        static bool failed;
        static Func<Map, bool> isSkyBlockMap;

        internal static bool Active => ModsConfig.IsActive("yexiaoyang.rsbskyland");

        internal static bool AllowsIncidents(Map map)
        {
            if (map == null)
            {
                return false;
            }

            if (!Active || (Core.RHAH_Mod.Settings != null && Core.RHAH_Mod.Settings.skylandIncidentsEnabled))
            {
                return true;
            }

            try
            {
                if (!resolved)
                {
                    resolved = true;
                    Type type = AccessTools.TypeByName("RimSkyBlock_Skyland.SkyBlockStartupUtility");
                    MethodInfo method = type?.GetMethod("IsSkyBlockMap", BindingFlags.Static | BindingFlags.Public,
                        null, new[] { typeof(Map) }, null);
                    if (type == null || !type.IsPublic || method?.ReturnType != typeof(bool) || method.ContainsGenericParameters)
                    {
                        throw new InvalidOperationException("Skyland public map API does not match the supported contract");
                    }

                    isSkyBlockMap = (Func<Map, bool>)Delegate.CreateDelegate(typeof(Func<Map, bool>), method);
                }

                return isSkyBlockMap != null && !isSkyBlockMap(map);
            }
            catch (Exception exception)
            {
                isSkyBlockMap = null;
                if (!failed)
                {
                    failed = true;
                    Log.Error("[RHAH] Skyland map API unavailable; pausing map incidents while sky island incidents are disabled: " + exception);
                }
                return false;
            }
        }
    }
}
