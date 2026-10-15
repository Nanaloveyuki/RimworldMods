using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace WashingMachine;

public class Building_WashingMachine : Building, IThingHolderWithDrawnPawn
{
    public const int CycleTicks = 1800;
    private ThingOwner<Thing> contents;
    private int remainingTicks;
    private bool cycleCompleted;
    private bool pendingDeath;
    private bool releasing;
    private CompPowerTrader power;
    private CompFlickable flick;

    public Building_WashingMachine() => contents = new ThingOwner<Thing>(this, true);
    public bool Busy => contents.Count != 0;
    public Pawn Occupant => Busy ? contents[0] as Pawn : null;
    public int RemainingTicks => remainingTicks;
    public bool HoldingPawn => Occupant != null && !releasing;
    public bool HasPower => Spawned && (flick == null || flick.SwitchIsOn)
        && (WashingMachineMod.Settings.PowerWatts == 0f || power?.PowerOn == true);
    public float HeldPawnDrawPos_Y => AltitudeLayer.BuildingOnTop.AltitudeFor() + 0.05f;
    public float HeldPawnBodyAngle => -((CycleTicks - remainingTicks) % 30) * 12f;
    public PawnPosture HeldPawnPosture => PawnPosture.LayingOnGroundNormal;

    public ThingOwner GetDirectlyHeldThings() => contents;
    public void GetChildHolders(List<IThingHolder> outChildren) =>
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, contents);

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Deep.Look(ref contents, "contents", this);
        Scribe_Values.Look(ref remainingTicks, "remainingTicks");
        Scribe_Values.Look(ref cycleCompleted, "cycleCompleted");
        Scribe_Values.Look(ref pendingDeath, "pendingDeath");
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        power = GetComp<CompPowerTrader>();
        flick = GetComp<CompFlickable>();
        RefreshPower();
    }

    public void RefreshPower()
    {
        if (power != null)
            power.PowerOutput = -WashingMachineMod.Settings.PowerWatts;
    }

    public bool CanAcceptPawn(Pawn target) => Spawned && !Busy && target != null
        && !target.Destroyed && !target.Dead && target.BodySize <= WashingMachineMod.Settings.MaxBodySize;

    public bool TryLoadPawn(Pawn carrier, Pawn target)
    {
        if (!CanAcceptPawn(target) || carrier.carryTracker.CarriedThing != target)
            return false;
        if (carrier.carryTracker.innerContainer.TryTransferToContainer(target, contents, 1) != 1)
            return false;
        BeginCycle();
        EnsureWashJob(target);
        return true;
    }

    public bool TryLoadApparel(Pawn carrier, Apparel item)
    {
        if (!Spawned || Busy || item == null || carrier.carryTracker.CarriedThing != item)
            return false;
        if (carrier.carryTracker.innerContainer.TryTransferToContainer(item, contents, item.stackCount) == 0)
            return false;
        BeginCycle();
        return true;
    }

    private void BeginCycle()
    {
        remainingTicks = CycleTicks;
        cycleCompleted = false;
    }

    private void EnsureWashJob(Pawn target)
    {
        if (target.Dead || target.jobs == null || target.CurJobDef == WashingMachineDefOf.PWM_BeingWashed)
            return;
        Job forced = JobMaker.MakeJob(WashingMachineDefOf.PWM_BeingWashed, this);
        forced.playerForced = true;
        target.jobs.StartJob(forced, JobCondition.InterruptForced);
    }

    protected override void Tick()
    {
        base.Tick();
        // CompPowerTrader can be reinitialized when the grid reconnects.
        RefreshPower();
        if (!Busy)
            return;
        if (cycleCompleted)
        {
            ReleaseContents();
            return;
        }
        Pawn target = Occupant;
        if (target != null)
            EnsureWashJob(target);
        contents.DoTick();
        if (target != null && (target.Dead || target.Destroyed || Occupant != target))
        {
            CancelCycle();
            return;
        }
        if (!HasPower)
            return;
        if (--remainingTicks > 0)
            return;
        // Commit before ejecting: a failed placement must not roll mood/death again.
        cycleCompleted = true;
        if (Busy && contents[0] is Apparel item)
            item.WornByCorpse = false;
        if (target != null)
        {
            Compatibility.ApplyAfterWash(target);
            pendingDeath = target.BodySize < 0.25f && Rand.Chance(WashingMachineMod.Settings.SmallPawnDeathChance);
        }
        ReleaseContents();
    }

    public void CancelCycle()
    {
        remainingTicks = 0;
        cycleCompleted = true;
        ReleaseContents();
    }

    private bool ReleaseContents()
    {
        if (!Spawned || !Busy)
            return !Busy;
        Pawn target = Occupant;
        releasing = true;
        try
        {
            if (!contents.TryDrop(contents[0], InteractionCell, Map, ThingPlaceMode.Near, out Thing dropped))
                return false;
            if (target?.CurJobDef == WashingMachineDefOf.PWM_BeingWashed)
                target.jobs.EndCurrentJob(JobCondition.Succeeded, startNewJob: false);
            remainingTicks = 0;
            cycleCompleted = false;
            if (pendingDeath && target != null && !target.Dead)
                target.Kill(null);
            pendingDeath = false;
            return true;
        }
        finally { releasing = false; }
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        CancelCycle();
        // Direct placement on this walkable footprint is a last resort, never delete the contents.
        while (Busy)
        {
            Thing item = contents[0];
            contents.Remove(item);
            GenSpawn.Spawn(item, Position, Map);
            if (item is Pawn target && target.CurJobDef == WashingMachineDefOf.PWM_BeingWashed)
                target.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);
        }
        base.DeSpawn(mode);
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        base.DrawAt(drawLoc, flip);
        Pawn target = Occupant;
        if (target == null || target.Dead)
            return;
        PawnRenderer renderer = target.Drawer.renderer;
        renderer.EnsureGraphicsInitialized();
        PawnDrawParms parms = PawnDrawParms.DefaultFor(target);
        parms.facing = Rot4.South;
        parms.posture = HeldPawnPosture;
        parms.flags |= PawnRenderFlags.NeverAimWeapon;
        Vector3 center = drawLoc;
        center.y = HeldPawnDrawPos_Y;
        center.z -= 0.07f;
        parms.matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(HeldPawnBodyAngle, Vector3.up), Vector3.one * 0.42f);
        renderer.renderTree.ParallelPreDraw(parms);
        renderer.renderTree.Draw(parms);
    }

    public override string GetInspectString()
    {
        string status = !HasPower ? "PWM_NoPower".Translate().ToString()
            : Busy ? "PWM_Progress".Translate((remainingTicks / 60f).ToString("F1")).ToString()
            : "PWM_Idle".Translate().ToString();
        if (Busy)
            status += "\n" + contents[0].LabelCap;
        string original = base.GetInspectString();
        return original.NullOrEmpty() ? status : original + "\n" + status;
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (Gizmo gizmo in base.GetGizmos())
            yield return gizmo;
        if (Occupant != null)
        {
            Gizmo select = Building.SelectContainedItemGizmo(this, Occupant);
            if (select != null)
                yield return select;
        }
        if (Busy)
            yield return new Command_Action { defaultLabel = "PWM_Cancel".Translate(), action = CancelCycle,
                icon = TexCommand.ClearPrioritizedWork };
    }

    public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
    {
        foreach (FloatMenuOption option in base.GetFloatMenuOptions(selPawn))
            yield return option;
        if (Busy || selPawn.carryTracker == null || selPawn.Downed)
            yield break;
        foreach (Thing thing in Map.listerThings.ThingsInGroup(ThingRequestGroup.Apparel))
        {
            if (thing is Apparel item && item.WornByCorpse && item.Spawned
                && selPawn.CanReserveAndReach(item, PathEndMode.Touch, Danger.Deadly)
                && selPawn.CanReserveAndReach(this, PathEndMode.Touch, Danger.Deadly))
            {
                Apparel chosen = item;
                yield return new FloatMenuOption("PWM_LoadApparel".Translate() + ": " + item.LabelCap,
                    () => selPawn.jobs.TryTakeOrderedJob(JobMaker.MakeJob(WashingMachineDefOf.PWM_LoadApparel, chosen, this)));
            }
        }
    }
}
