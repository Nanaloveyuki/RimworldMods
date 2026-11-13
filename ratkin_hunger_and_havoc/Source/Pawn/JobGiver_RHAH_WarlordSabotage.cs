using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using HungerAndHavoc.Incidents;
using HungerAndHavoc.Pawn.Compat;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public sealed class JobGiver_RHAH_WarlordSabotage : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            if (!CanAct(pawn)) return null;
            Thing target = GenClosest.ClosestThingReachable(pawn.Position, pawn.Map,
                ThingRequest.ForGroup(ThingRequestGroup.BuildingArtificial), PathEndMode.Touch,
                TraverseParms.For(pawn, Danger.Some), 9999f, ValidTarget);
            if (target == null) return JobMaker.MakeJob(JobDefOf.Wait, 120);
            return CreateJob(target, Rand.Value);
        }

        internal static bool CanAct(Verse.Pawn pawn)
        {
            if (pawn == null || !pawn.Spawned || pawn.Dead || pawn.IsPrisoner || pawn.IsSlave ||
                pawn.Faction == null || pawn.Faction.IsPlayer || !pawn.DevelopmentalStage.Baby() ||
                (pawn.MentalStateDef?.stopsJobs ?? false) || (pawn.stances?.stunner?.Stunned ?? false) ||
                !RHAH_Api.IsVisitor(pawn)) return false;
            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            return snapshot?.SourceIncidentDisplayId == RHAH_WarlordCompat.SuicideSquad &&
                snapshot.Lifecycle != RHAH_Lifecycle.Leaving && snapshot.Attitude == RHAH_Attitude.Hostile &&
                pawn.Faction.HostileTo(Faction.OfPlayer) && RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Fight) &&
                RHAH_ChildMovement.CanWalkOut(pawn);
        }

        internal static bool ValidTarget(Thing target)
        {
            return target is Building && target.Spawned && !target.Destroyed && target.Faction?.IsPlayer == true &&
                target.def.useHitPoints && target.HitPoints > 0 && !target.IsBurning();
        }

        internal static Job CreateJob(Thing target, float roll)
        {
            JobDef def = target.FlammableNow && roll < 0.35f
                ? RHAH_DefOf.RHAH_WarlordIgnite : RHAH_DefOf.RHAH_WarlordVandalize;
            return JobMaker.MakeJob(def, target);
        }
    }
}
