using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    internal sealed class JobGiver_RHAH_DigOutWhenNoExit : JobGiver_ExitMapBest
    {
        static readonly JobGiver_RHAH_DigOutWhenNoExit sharedGiver = new JobGiver_RHAH_DigOutWhenNoExit();

        JobGiver_RHAH_DigOutWhenNoExit()
        {
            defaultLocomotion = LocomotionUrgency.Jog;
        }

        internal static Job TryCreate(Verse.Pawn pawn)
        {
            return sharedGiver.TryGiveJob(pawn);
        }
    }
}
