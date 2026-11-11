using RimWorld;
using Verse;

namespace LeadYourPet
{
    internal static class BabyAccompaniedExitCompat
    {
        private const string PackageId = "hawks.babyaccompaniedexit";

        internal static bool TryExitAlongsideAdultMaster(Pawn pawn, Pawn master, Map map, Rot4 exitDir)
        {
            if (ModLister.GetActiveModWithIdentifier(PackageId, false) == null
                || pawn?.RaceProps.Humanlike != true
                || pawn.ageTracker == null
                || pawn.ageTracker.AgeBiologicalYears >= 3
                || pawn.Dead
                || pawn.InMentalState
                || (pawn.Downed && !LifeStageUtility.AlwaysDowned(pawn))
                || pawn.CarriedBy != null
                || !pawn.Spawned
                || map == null
                || pawn.Map != map
                || !map.CanEverExit
                || master?.RaceProps.Humanlike != true
                || master.ageTracker?.AgeBiologicalYears < 3
                || !master.Spawned
                || master.Map != map
                || pawn.Faction == null
                || pawn.Faction != master.Faction)
            {
                return false;
            }

            pawn.ExitMap(false, exitDir);
            return true;
        }
    }
}
