using HungerAndHavoc.Core;
using HungerAndHavoc.Trade;
using HungerAndHavoc.Api;
using RimWorld;
using Verse;
using Verse.AI.Group;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_Visitor : ThinkNode_JobGiver
    {
        static readonly VisitorFight fight = new VisitorFight();

        internal class VisitorFight : JobGiver_AIFightEnemies
        {

            protected override bool ExtraTargetValidator(Verse.Pawn pawn, Thing target)
            {
                return (!RHAH_MouseSwarm.IsProvoked(pawn) || target.Faction == Faction.OfPlayer) &&
                    base.ExtraTargetValidator(pawn, target);
            }

            protected override bool ShouldLoseTarget(Verse.Pawn pawn)
            {
                return (RHAH_MouseSwarm.IsProvoked(pawn) && pawn.mindState.enemyTarget?.Faction != Faction.OfPlayer) ||
                    base.ShouldLoseTarget(pawn);
            }
            internal Job TryCreate(Verse.Pawn pawn)
            {
                bool swarm = RHAH_MouseSwarm.IsProvoked(pawn);
                targetAcquireRadius = swarm ? 9999f : 56f;
                targetKeepRadius = swarm ? 9999f : 65f;
                return CanFight(pawn) ? base.TryGiveJob(pawn) : null;
            }
        }

        internal static Job TryFight(Verse.Pawn pawn)
        {
            return fight.TryCreate(pawn);
        }

        internal static bool CanFight(Verse.Pawn pawn)
        {
            LordJob_RHAH_Visitor visitor = pawn?.GetLord()?.LordJob as LordJob_RHAH_Visitor;
            if (visitor?.ArmedChoiceId > 0 && !RHAH_ArmedDemand.Enabled) return false;
            if (visitor != null && pawn.GetLord().CurLordToil is LordToil_RHAH_WaitFood && visitor.ListedForFood(pawn))
            {
                return false;
            }
            return RHAH_BatchAttitude.CanOrderLeave(pawn) && pawn.Spawned && !pawn.Downed &&
                !(pawn.stances?.stunner?.Stunned ?? false) && pawn.DevelopmentalStage.Adult() &&
                RHAH_Api.Get(pawn).Attitude == RHAH_Attitude.Hostile &&
                pawn.Faction != null && pawn.Faction.HostileTo(Faction.OfPlayer) &&
                RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Fight) &&
                !pawn.WorkTagIsDisabled(WorkTags.Violent);
        }
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            if (!RHAH_BatchAttitude.CanOrderLeave(pawn))
            {
                return null;
            }
            if (RHAH_MouseSwarm.IsProvoked(pawn))
            {
                if (pawn.Downed || pawn.jobs == null || !RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Fight)) return null;
                Job combat = fight.TryCreate(pawn);
                return combat ?? JobMaker.MakeJob(JobDefOf.Wait_Combat, 120, true);
            }
            Job longWait = HungerAndHavoc.EventMgr.RHAH_LongFollowRuntime.EdgeJob(pawn);
            if (longWait != null) return longWait;

            MarkSeekingFood(pawn);

            Job attack = fight.TryCreate(pawn);
            if (attack != null)
            {
                return attack;
            }
            // 索粮进攻只走原版进攻 duty 不再回落啃食或乞讨
            if (pawn.GetLord()?.CurLordToil is LordToil_RHAH_ArmedAssault) return null;

            Job departure = JobGiver_RHAH_Leave.TryCreate(pawn);
            if (departure != null || RHAH_Api.Get(pawn).Lifecycle == RHAH_Lifecycle.Leaving)
            {
                return departure;
            }
            Job swarm = RHAH_MouseSwarm.TryCreateJob(pawn);
            if (swarm != null) return swarm;


            Job feedChild = JobGiver_RHAH_MotherFeed.TryCreate(pawn);
            if (feedChild != null)
            {
                return feedChild;
            }

            if (RHAH_Api.Allows(pawn, RHAH_BehaviorGate.FeedFromRelief))
            {
                Job feed = JobGiver_RHAH_Feed.TryCreate(pawn);
                if (feed != null)
                {
                    return feed;
                }
            }

            if (RHAH_Begging.PrefersGnaw(pawn))
            {
                Job gnaw = JobGiver_RHAH_Gnaw.TryCreate(pawn, true);
                if (gnaw != null)
                {
                    return gnaw;
                }
            }

            if (RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Beg))
            {
                Job beg = JobGiver_RHAH_Beg.TryCreate(pawn);
                if (beg != null)
                {
                    return beg;
                }
            }

            if (RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Steal))
            {
                Job steal = JobGiver_RHAH_Steal.TryCreate(pawn);
                if (steal != null)
                {
                    return steal;
                }
            }



            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.EatOutsideRelief))
            {
                Job wait = JobGiver_RHAH_WaitFood.TryCreate(pawn);
                if (wait != null)
                {
                    return wait;
                }
            }


            return null;
        }

        internal static void MarkSeekingFood(Verse.Pawn pawn)
        {
            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot != null && snapshot.Lifecycle == RHAH_Lifecycle.Arriving)
            {
                RHAH_Api.SetLifecycle(pawn, RHAH_Lifecycle.SeekingFood);
            }
        }
    }
}
