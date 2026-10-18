using RimWorld;
using Verse;
using VersePawn = Verse.Pawn;

namespace HungerAndHavoc.Generation
{
    internal static class RHAH_PawnBody
    {
        internal const float MinimumBloodLoss = 0.7f;

        internal static void Shatter(VersePawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            RemovePart(pawn, "Ear", "left");
            RemovePart(pawn, "Leg", "right");
            RemovePart(pawn, "RK_RatTail", null);
            if (pawn.Map != null)
            {
                HealthUtility.DamageUntilDowned(pawn, false);
            }

            Hediff bloodLoss = pawn.health.GetOrAddHediff(HediffDefOf.BloodLoss);
            if (bloodLoss != null && bloodLoss.Severity < MinimumBloodLoss)
            {
                bloodLoss.Severity = MinimumBloodLoss;
            }
        }

        internal static void StartLabor(VersePawn pawn)
        {
            if (pawn?.health == null || !ModsConfig.BiotechActive || HediffDefOf.PregnantHuman == null)
            {
                return;
            }

            Hediff existing = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.PregnantHuman);
            Hediff_Pregnant pregnancy = existing as Hediff_Pregnant;
            if (pregnancy == null)
            {
                pregnancy = HediffMaker.MakeHediff(HediffDefOf.PregnantHuman, pawn) as Hediff_Pregnant;
                if (pregnancy == null)
                {
                    return;
                }

                pregnancy.Severity = 0.9f;
                pregnancy.SetParents(pawn, null, PregnancyUtility.GetInheritedGeneSet(null, pawn));
                pawn.health.AddHediff(pregnancy);
            }

            pregnancy.StartLabor();
            pawn.health.RemoveHediff(pregnancy);
        }

        static void RemovePart(VersePawn pawn, string defName, string labelContains)
        {
            BodyPartRecord part = null;
            foreach (BodyPartRecord record in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (record?.def == null || record.def.defName != defName)
                {
                    continue;
                }

                if (labelContains != null &&
                    (record.customLabel == null || record.customLabel.IndexOf(labelContains, System.StringComparison.OrdinalIgnoreCase) < 0))
                {
                    continue;
                }

                part = record;
                break;
            }

            if (part != null && HediffDefOf.MissingBodyPart != null)
            {
                pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
            }
        }
    }
}
