using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn
{
    public class StatPart_RHAH_TemperatureApparel : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Thing thing = req.Thing;
            if (thing == null || parentStat == null)
            {
                return;
            }

            float offset = Offset(thing.def?.defName, parentStat.defName);
            if (offset != 0f)
            {
                val += offset;
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            return null;
        }

        internal static float Offset(string defName, string statName)
        {
            if (!RHAH_VisitorRules.IsTemperatureApparel(defName) || Core.RHAH_Mod.Settings == null)
            {
                return 0f;
            }

            bool cold = statName == "Insulation_Cold";
            if (statName != "Insulation_Cold" && statName != "Insulation_Heat")
            {
                return 0f;
            }

            bool coldApparel = System.Array.IndexOf(RHAH_VisitorRules.ColdApparel, defName) >= 0;
            if (cold != coldApparel)
            {
                return 0f;
            }

            return Core.RHAH_Mod.Settings.TemperatureApparelInsulation(defName) - RHAH_VisitorRules.DefaultInsulation(defName);
        }
    }
}
