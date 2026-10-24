using RimWorld;
using Verse;

namespace PrisonersPayToEat2
{
    public static class PrisonerMedicalRestPolicy
    {
        public static bool ShouldSeekMedicalRest(Pawn pawn)
        {
            return HealthAIUtility.ShouldSeekMedicalRest(pawn) && AllowsMedicalRest(pawn);
        }

        public static bool AllowsMedicalRest(Pawn pawn)
        {
            var settings = PrisonersPayToEat2Mod.Settings;
            if (settings == null || (!settings.forbidPrisonerMedicalRest && !settings.forbidNonCriticalPrisonerMedicalRest))
                return true;
            if (pawn == null || !pawn.IsPrisonerOfColony || pawn.Dead || pawn.Downed)
                return true;

            // Getting into bed for an available doctor is treatment, not voluntary recuperation.
            if (HealthAIUtility.ShouldHaveSurgeryDoneNow(pawn)
                || (HealthAIUtility.ShouldBeTendedNowByPlayer(pawn) && WorkGiver_PatientGoToBedTreatment.AnyAvailableDoctorFor(pawn)))
                return true;

            if (settings.forbidPrisonerMedicalRest)
                return false;
            return HasLifeThreateningCondition(pawn, settings.criticalDiseaseThreshold);
        }

        public static bool HasLifeThreateningCondition(Pawn pawn, float criticalDiseaseThreshold)
        {
            var hediffSet = pawn?.health?.hediffSet;
            if (hediffSet == null)
                return false;

            if (HealthUtility.TicksUntilDeathDueToBloodLoss(pawn) < int.MaxValue)
                return true;

            var hediffs = hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff.ShouldRemove || hediff.FullyImmune() || hediff.def == HediffDefOf.BloodLoss)
                    continue;

                if (hediff.IsLethal && hediff.Severity / hediff.def.lethalSeverity > criticalDiseaseThreshold)
                    return true;

                // Lethal immunizable diseases must not bypass the slider through an earlier stage.
                if ((!hediff.IsLethal || hediff.TryGetComp<HediffComp_Immunizable>() == null)
                    && hediff.IsCurrentlyLifeThreatening)
                    return true;
            }
            return false;
        }
    }
}
