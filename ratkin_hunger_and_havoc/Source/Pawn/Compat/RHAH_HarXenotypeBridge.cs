using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    internal static class RHAH_HarXenotypeBridge
    {
        static bool resolved;
        static Func<XenotypeDef, ThingDef, bool> canUse;

        internal static bool Allows(XenotypeDef xenotype, ThingDef race)
        {
            if (xenotype == null || race == null)
            {
                return false;
            }

            try
            {
                if (!resolved)
                {
                    resolved = true;
                    Type restrictions = AccessTools.TypeByName("AlienRace.RaceRestrictionSettings");
                    MethodInfo method = restrictions?.GetMethod("CanUseXenotype", BindingFlags.Public | BindingFlags.Static,
                        null, new[] { typeof(XenotypeDef), typeof(ThingDef) }, null);
                    if (method != null)
                    {
                        canUse = (Func<XenotypeDef, ThingDef, bool>)Delegate.CreateDelegate(
                            typeof(Func<XenotypeDef, ThingDef, bool>), method);
                    }
                }

                if (canUse != null)
                {
                    return canUse(xenotype, race);
                }

                Log.ErrorOnce("[RHAH] HAR public CanUseXenotype API is unavailable; pawn generation is stopped.", 104831701);
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[RHAH] HAR xenotype permission query failed; pawn generation is stopped. " + exception, 104831702);
            }

            return false;
        }
    }
}
