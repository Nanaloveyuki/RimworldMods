using HarmonyLib;
using HungerAndHavoc.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace HungerAndHavoc.Pawn
{
    [HarmonyPatch(typeof(Trait), nameof(Trait.LabelCap), MethodType.Getter)]
    internal static class RHAH_TraitColor
    {
        static void Postfix(Trait __instance, ref string __result)
        {
            if (__instance?.def == null || string.IsNullOrEmpty(__result) ||
                __result.IndexOf("<color", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                !RHAH_ContentCatalog.IsOwnedTrait(__instance.def.defName))
            {
                return;
            }

            RHAH_TraitRecord record = Find(__instance.def.defName);
            if (record == null)
            {
                return;
            }

            __result = __result.Colorize(new Color(record.Red, record.Green, record.Blue));
        }

        static RHAH_TraitRecord Find(string defName)
        {
            System.Collections.Generic.IReadOnlyList<RHAH_TraitRecord> traits = RHAH_ContentCatalog.Traits;
            for (int i = 0; i < traits.Count; i++)
            {
                if (traits[i].TraitDefName == defName)
                {
                    return traits[i];
                }
            }

            return null;
        }
    }
}
