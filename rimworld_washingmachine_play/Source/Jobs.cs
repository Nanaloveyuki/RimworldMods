using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace WashingMachine;

public class JobDriver_LoadApparel : JobDriver
{
    private Building_WashingMachine Washer => job.targetB.Thing as Building_WashingMachine;
    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
        && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnDespawnedOrNull(TargetIndex.B);
        this.FailOn(() => Washer == null || Washer.Busy);
        yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
            .FailOnDespawnedNullOrForbidden(TargetIndex.A);
        Toil carry = Toils_Haul.StartCarryThing(TargetIndex.A);
        carry.AddPreInitAction(() => job.count = 1);
        yield return carry;
        yield return Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
        Toil load = ToilMaker.MakeToil("LoadWasherApparel");
        load.initAction = () =>
        {
            if (!Washer.TryLoadApparel(pawn, pawn.carryTracker.CarriedThing as Apparel))
                EndJobWith(JobCondition.Incompletable);
        };
        load.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return load;
    }
}

public class JobDriver_CarryToWasher : JobDriver
{
    private Pawn Takee => job.targetA.Thing as Pawn;
    private Building_WashingMachine Washer => job.targetB.Thing as Building_WashingMachine;

    public override bool TryMakePreToilReservations(bool errorOnFailed) =>
        pawn.Reserve(job.targetA, job, 1, -1, null, errorOnFailed)
        && pawn.Reserve(job.targetB, job, 1, -1, null, errorOnFailed);

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDestroyedOrNull(TargetIndex.A);
        this.FailOnDespawnedOrNull(TargetIndex.B);
        this.FailOn(() => Washer == null || !Washer.CanAcceptPawn(Takee));
        Toil approach = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch)
            .FailOnDespawnedNullOrForbidden(TargetIndex.A);
        Toil destination = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
        yield return Toils_Jump.JumpIf(destination, () => pawn.carryTracker.CarriedThing == Takee);
        yield return approach;
        Toil pickup = ToilMaker.MakeToil("PickUpWasherPawn");
        pickup.initAction = () =>
        {
            // The washer deliberately permits awake creatures; do not use rescue/downed eligibility.
            if (pawn.carryTracker.CarriedThing != null || Takee.Dead
                || pawn.carryTracker.TryStartCarry(Takee, 1) != 1)
                EndJobWith(JobCondition.Incompletable);
        };
        pickup.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return pickup;
        yield return destination;
        Toil load = ToilMaker.MakeToil("LoadWasherPawn");
        load.initAction = () =>
        {
            if (!Washer.TryLoadPawn(pawn, Takee))
                EndJobWith(JobCondition.Incompletable);
        };
        load.defaultCompleteMode = ToilCompleteMode.Instant;
        yield return load;
    }
}

public class JobDriver_BeingWashed : JobDriver
{
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;
    protected override IEnumerable<Toil> MakeNewToils()
    {
        Toil wash = ToilMaker.MakeToil("BeingWashed");
        wash.initAction = () =>
        {
            pawn.pather?.StopDead();
            pawn.jobs.posture = PawnPosture.LayingOnGroundNormal;
        };
        wash.defaultCompleteMode = ToilCompleteMode.Never;
        wash.AddEndCondition(() => pawn.ParentHolder is Building_WashingMachine washer && washer.HoldingPawn
            ? JobCondition.Ongoing : JobCondition.Succeeded);
        yield return wash;
    }
}

public class WorkGiver_LoadTaintedApparel : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest => ThingRequest.ForGroup(ThingRequestGroup.Apparel);
    public override PathEndMode PathEndMode => PathEndMode.Touch;

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false) =>
        t is Apparel item && item.WornByCorpse && item.Spawned && !item.IsForbidden(pawn)
        && pawn.CanReserveAndReach(item, PathEndMode.Touch, Danger.Deadly)
        && WasherUtility.FindAvailableWasher(pawn) != null;

    public override Job JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        Building_WashingMachine washer = WasherUtility.FindAvailableWasher(pawn);
        return washer == null ? null : JobMaker.MakeJob(WashingMachineDefOf.PWM_LoadApparel, t, washer);
    }
}

public static class WasherUtility
{
    public static bool HasKnownWasher(Map map)
    {
        if (map == null)
            return false;
        List<Thing> buildings = map.listerThings.ThingsOfDef(WashingMachineDefOf.PWM_WashingMachine);
        for (int i = 0; i < buildings.Count; i++)
            if (IsKnownWasher(buildings[i], map))
                return true;
        return false;
    }

    private static bool IsKnownWasher(Thing thing, Map map) => thing is Building_WashingMachine
        && thing.Spawned && thing.Map == map && !thing.Fogged() && thing.Faction == Faction.OfPlayer;

    public static Building_WashingMachine FindAvailableWasher(Pawn carrier, Pawn target = null)
    {
        if (carrier?.Map == null || carrier.carryTracker == null || carrier.Downed || carrier.Dead)
            return null;
        Building_WashingMachine nearest = null;
        int distance = int.MaxValue;
        List<Thing> buildings = carrier.Map.listerThings.ThingsOfDef(WashingMachineDefOf.PWM_WashingMachine);
        for (int i = 0; i < buildings.Count; i++)
        {
            if (!IsKnownWasher(buildings[i], carrier.Map))
                continue;
            Building_WashingMachine washer = (Building_WashingMachine)buildings[i];
            int candidateDistance = (washer.Position - carrier.Position).LengthHorizontalSquared;
            if (candidateDistance >= distance || washer.Busy || !washer.HasPower || washer.IsForbidden(carrier)
                || (target != null && !washer.CanAcceptPawn(target))
                || !carrier.CanReserveAndReach(washer, PathEndMode.Touch, Danger.Deadly))
                continue;
            nearest = washer;
            distance = candidateDistance;
        }
        return nearest;
    }
}

public class FloatMenuOptionProvider_Washer : FloatMenuOptionProvider
{
    protected override bool Drafted => true;
    protected override bool Undrafted => true;
    protected override bool Multiselect => false;
    protected override bool RequiresManipulation => true;

    protected override bool AppliesInt(FloatMenuContext context) => WasherUtility.HasKnownWasher(context.map);

    protected override FloatMenuOption GetSingleOptionFor(Pawn clickedPawn, FloatMenuContext context)
    {
        Pawn carrier = context.FirstSelectedPawn;
        if (!WasherUtility.HasKnownWasher(context.map) || carrier == null || clickedPawn.Dead || clickedPawn == carrier)
            return null;
        string label = "PWM_CarryPawn".Translate(clickedPawn.LabelShort);
        if (clickedPawn.BodySize > WashingMachineMod.Settings.MaxBodySize)
            return new FloatMenuOption(label + " (" + "PWM_TooLarge".Translate(WashingMachineMod.Settings.MaxBodySize.ToString("F2")) + ")", null);
        Building_WashingMachine washer = WasherUtility.FindAvailableWasher(carrier, clickedPawn);
        if (washer == null || !carrier.CanReserveAndReach(clickedPawn, PathEndMode.Touch, Danger.Deadly)
            || carrier.carryTracker.AvailableStackSpace(clickedPawn.def) < 1)
            return new FloatMenuOption(label + " (" + "PWM_CannotCarry".Translate() + ")", null);
        return new FloatMenuOption(label, () => carrier.jobs.TryTakeOrderedJob(
            JobMaker.MakeJob(WashingMachineDefOf.PWM_CarryToWasher, clickedPawn, washer)));
    }
}
