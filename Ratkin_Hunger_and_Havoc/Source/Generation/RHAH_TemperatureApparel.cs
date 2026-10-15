using System.Collections.Generic;
using HungerAndHavoc.Core;
using HungerAndHavoc.Pawn;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_TemperatureApparel
    {
        internal static string Select(float temperature, float comfortableMinimum, float comfortableMaximum, RHAH_Settings settings)
        {
            if (settings == null || !settings.coldClothesEnabled)
            {
                return null;
            }

            int direction = RHAH_VisitorRules.TemperatureDirection(temperature, comfortableMinimum, comfortableMaximum);
            float required = RHAH_VisitorRules.RequiredInsulation(
                direction,
                temperature,
                comfortableMinimum,
                comfortableMaximum,
                settings.minimumEventTemperature,
                settings.maximumEventTemperature);
            return RHAH_VisitorRules.SelectTemperatureApparel(
                direction,
                required,
                settings.IsTemperatureApparelEnabled,
                settings.TemperatureApparelInsulation);
        }

        internal static void Apply(Verse.Pawn pawn, float temperature)
        {
            RHAH_Settings settings = RHAH_Mod.Settings;
            if (pawn?.apparel == null || settings == null || HasTemperatureApparel(pawn))
            {
                return;
            }

            if (RHAH_VisitorRules.ClearsApparel(settings.apparelMode, false))
            {
                return;
            }

            float minimum = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin, true);
            float maximum = pawn.GetStatValue(StatDefOf.ComfyTemperatureMax, true);
            string selected = Select(temperature, minimum, maximum, settings);
            ThingDef def = string.IsNullOrEmpty(selected) ? null : DefDatabase<ThingDef>.GetNamedSilentFail(selected);
            if (def == null || !def.apparel.PawnCanWear(pawn, true) ||
                !ApparelUtility.HasPartsToWear(pawn, def) || !pawn.apparel.CanWearWithoutDroppingAnything(def))
            {
                return;
            }

            Apparel apparel = ThingMaker.MakeThing(def) as Apparel;
            if (apparel == null)
            {
                return;
            }

            pawn.apparel.Wear(apparel, false, false);
        }

        internal static bool HasTemperatureApparel(Verse.Pawn pawn)
        {
            List<Apparel> worn = pawn?.apparel?.WornApparel;
            if (worn == null)
            {
                return false;
            }

            for (int i = 0; i < worn.Count; i++)
            {
                if (RHAH_VisitorRules.IsTemperatureApparel(worn[i]?.def?.defName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
