using HarmonyLib;
using RimWorld;
using Verse;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(PregnancyUtility), nameof(PregnancyUtility.ApplyBirthOutcome))]
    public static class MouseDisasterRatkinBirthOutcomePatch
    {
        public static void Prefix(ref Pawn geneticMother, Thing birtherThing)
        {
            if (birtherThing is Pawn birtherPawn && MouseDisasterUtility.IsRatkin(birtherPawn) && !MouseDisasterUtility.IsRatkin(geneticMother))
            {
                geneticMother = birtherPawn;
            }
        }

        public static void Postfix(Thing __result, Pawn geneticMother, Thing birtherThing)
        {
            Pawn mother = birtherThing as Pawn ?? geneticMother;
            Pawn newborn = __result as Pawn ?? (__result as Corpse)?.InnerPawn;
        }
    }

    [HarmonyPatch(typeof(PawnUtility), nameof(PawnUtility.TrySpawnHatchedOrBornPawn))]
    public static class MouseDisasterBirthPatch
    {
        public static void Postfix(Pawn pawn, Thing motherOrEgg, bool __result)
        {
            if (!__result || pawn == null || !(motherOrEgg is Pawn parentPawn))
            {
                return;
            }

            MouseDisasterUtility.TryNormalizeColonyBornRatkinBabyBackstory(pawn, parentPawn);
            MouseDisasterUtility.TryAssignBirthMouseDisasterGenes(pawn, parentPawn);
            MouseDisasterUtility.TryInheritBirthStatus(pawn, parentPawn);
            MouseDisasterUtility.TryRemoveMouseDisasterFatherRelationAfterBirth(pawn, parentPawn);
            MouseDisasterUtility.TryGainBloodlineBirthThought(parentPawn);
            MouseDisasterUtility.RefreshRatkinDevelopmentalPresentation(pawn);
            MouseDisasterUtility.NotifyMouseDisasterPawnIdentityOrLifeStageChanged(pawn);
        }
    }
}
