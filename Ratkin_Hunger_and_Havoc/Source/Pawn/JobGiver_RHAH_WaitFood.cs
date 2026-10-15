using HungerAndHavoc.Identity;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_WaitFood : ThinkNode_JobGiver
    {
        internal const int WaitTicks = 30000;

        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        internal static Job TryCreate(Verse.Pawn pawn)
        {
            if (!RHAH_Api.IsVisitor(pawn) || pawn.Map == null || pawn.Downed)
            {
                return null;
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.FeedFromRelief))
            {
                return null;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot == null || snapshot.HasBeenFed || RHAH_ReliefFood.MayEatOutside(pawn))
            {
                return null;
            }

            if (JobDefOf.Wait == null)
            {
                return null;
            }

            RHAH_Settings settings = RHAH_Mod.Settings;
            bool wait = settings == null || settings.waitWhenNoFood;
            float days = settings == null ? RHAH_VisitorRules.DefaultNoFoodWaitDays : settings.noFoodWaitDays;
            int budget = RHAH_VisitorRules.WaitTicks(wait, days);
            if (budget <= 0)
            {
                return null;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            CompRHAH_Pawn comp = CompRHAH_Pawn.TryGet(pawn);
            int deadline = comp == null ? -1 : comp.State.foodWaitUntilTick;
            if (deadline >= 0 && now >= deadline)
            {
                return null;
            }

            if (comp != null && deadline < 0)
            {
                comp.SetFoodWait(RHAH_VisitorRules.NextWaitTick(now, -1, false, true, days));
            }


            IntVec3 spot = WaitSpot(pawn);
            if (!spot.IsValid)
            {
                return null;
            }

            if (spot != pawn.Position && JobDefOf.Goto != null)
            {
                Job gotoJob = JobMaker.MakeJob(JobDefOf.Goto, spot);
                gotoJob.expiryInterval = RHAH_ReliefFood.RetryBaseTicks;
                gotoJob.checkOverrideOnExpire = true;
                return gotoJob;
            }

            Job job = JobMaker.MakeJob(JobDefOf.Wait);
            job.expiryInterval = RHAH_ReliefFood.RetryBaseTicks;
            job.checkOverrideOnExpire = true;
            return job;
        }

        const int PathChecks = 4;

        static IntVec3 WaitSpot(Verse.Pawn pawn)
        {
            Area_RHAH_Relief area = RHAH_ReliefArea.Get(pawn.Map);
            if (area == null || area.TrueCount == 0)
            {
                return VisitorWaitSpot(pawn);
            }

            IntVec3[] picked = new IntVec3[PathChecks];
            float[] scores = new float[PathChecks];
            int count = 0;
            foreach (IntVec3 cell in area.ActiveCells)
            {
                if (!cell.Standable(pawn.Map))
                {
                    continue;
                }

                Insert(picked, scores, ref count, cell, cell.DistanceToSquared(pawn.Position));
            }

            for (int i = 0; i < count; i++)
            {
                if (pawn.CanReach(picked[i], PathEndMode.OnCell, Danger.Deadly))
                {
                    return picked[i];
                }
            }

            return VisitorWaitSpot(pawn);
        }

        static IntVec3 VisitorWaitSpot(Verse.Pawn pawn)
        {
            LordJob_RHAH_Visitor visitor = pawn.GetLord()?.LordJob as LordJob_RHAH_Visitor;
            IntVec3 spot = visitor == null ? IntVec3.Invalid : visitor.WaitSpot;
            return spot.IsValid && spot.InBounds(pawn.Map) && spot.Standable(pawn.Map) &&
                pawn.CanReach(spot, PathEndMode.OnCell, Danger.Deadly) ? spot : pawn.Position;
        }

        static void Insert(IntVec3[] picked, float[] scores, ref int count, IntVec3 cell, float distance)
        {
            int index = count < PathChecks ? count : PathChecks - 1;
            if (count == PathChecks && distance >= scores[index])
            {
                return;
            }

            while (index > 0 && distance < scores[index - 1])
            {
                picked[index] = picked[index - 1];
                scores[index] = scores[index - 1];
                index--;
            }

            picked[index] = cell;
            scores[index] = distance;
            if (count < PathChecks)
            {
                count++;
            }
        }
    }
}
