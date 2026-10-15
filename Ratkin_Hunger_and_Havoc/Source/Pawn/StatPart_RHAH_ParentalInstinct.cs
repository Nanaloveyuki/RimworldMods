using RimWorld;
using Verse;
using VersePawn = Verse.Pawn;

namespace HungerAndHavoc.Pawn
{
    // XML 属性入口
    public sealed class StatPart_RHAH_ParentalInstinct : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            if (req.Thing is VersePawn pawn)
            {
                val *= RHAH_ParentalGenes.StatFactor(pawn, parentStat);
            }
        }

        public override string ExplanationPart(StatRequest req)
        {
            if (!(req.Thing is VersePawn pawn))
            {
                return null;
            }

            float factor = RHAH_ParentalGenes.StatFactor(pawn, parentStat);
            if (factor == 1f)
            {
                return null;
            }

            string defName = parentStat == StatDefOf.MaxNutrition ? RHAH_ParentalGenes.MaternalInstinct : RHAH_ParentalGenes.PaternalInstinct;
            return RHAH_ParentalGenes.Find(pawn, defName).LabelCap + ": x" + factor.ToStringPercent();
        }
    }
}
