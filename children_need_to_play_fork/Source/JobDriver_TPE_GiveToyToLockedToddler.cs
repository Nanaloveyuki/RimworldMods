using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace ToddlersPlayExpanded
{
    /// <summary>
    /// Colonist picks up a plush toy and equips it on a crib-locked toddler.
    /// TargetA = toddler, TargetB = plush toy.
    /// </summary>
    public class JobDriver_TPE_GiveToyToLockedToddler : JobDriver
    {
        protected Pawn Toddler => (Pawn)job.targetA.Thing;
        protected Thing Plush => job.targetB.Thing;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(Toddler, job, 1, -1, null, errorOnFailed)
                && pawn.Reserve(Plush, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOnDestroyedOrNull(TargetIndex.B);
            this.FailOnBurningImmobile(TargetIndex.B);

            // Walk to plush and pick it up
            yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.ClosestTouch);
            yield return Toils_Haul.StartCarryThing(TargetIndex.B);

            // Walk to toddler
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            // Equip plush on toddler
            yield return new Toil
            {
                initAction = delegate
                {
                    if (pawn.carryTracker.CarriedThing != Plush)
                    {
                        if (TPE_Mod.Settings?.debugLogging ?? false)
                            Log.Warning($"[TPE] {pawn} expected to carry {Plush} but is carrying {pawn.carryTracker.CarriedThing}");
                        return;
                    }

                    // Remove from carry tracker before equipping
                    pawn.carryTracker.innerContainer.Remove(Plush);

                    // Directly equip on the toddler
                    Toddler.apparel.Wear((Apparel)Plush, true);
                },
                defaultCompleteMode = ToilCompleteMode.Instant,
                handlingFacing = true
            };
        }
    }
}
