using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobGiver_RHAH_Beg : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Verse.Pawn pawn)
        {
            return TryCreate(pawn);
        }

        // 供无 Lord 的总 JobGiver 调度
        internal static Job TryCreate(Verse.Pawn pawn)
        {
            if (!RHAH_Api.IsVisitor(pawn))
            {
                return null;
            }

            if (!RHAH_Api.Allows(pawn, RHAH_BehaviorGate.Beg))
            {
                return null;
            }

            IRHAH_Pawn snapshot = RHAH_Api.Get(pawn);
            if (snapshot != null && snapshot.HasBeenFed)
            {
                return null;
            }

            if (RHAH_Begging.PrefersGnaw(pawn))
            {
                return null;
            }

            if (pawn.Map == null || pawn.Downed)
            {
                return null;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            if (!RHAH_Begging.CanBegAgain(pawn, now))
            {
                return null;
            }

            Verse.Pawn colonist = FindClosestColonist(pawn);
            if (colonist == null)
            {
                RHAH_Begging.StartFailCooldown(pawn, now, RHAH_Begging.CooldownHours());
                return null;
            }


            return JobMaker.MakeJob(RHAH_DefOf.RHAH_Beg, colonist);
        }

        const int PathChecks = 3;

        static Verse.Pawn FindClosestColonist(Verse.Pawn pawn)
        {
            Verse.Pawn[] picked = new Verse.Pawn[PathChecks];
            float[] scores = new float[PathChecks];
            int count = 0;
            foreach (Verse.Pawn colonist in pawn.Map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist == null || colonist == pawn)
                {
                    continue;
                }

                if (!RHAH_Begging.CanReceive(pawn, colonist, true, true))
                {
                    continue;
                }

                Insert(picked, scores, ref count, colonist, colonist.Position.DistanceToSquared(pawn.Position));
            }

            for (int i = 0; i < count; i++)
            {
                Verse.Pawn colonist = picked[i];
                bool reachable = pawn.CanReach(colonist, PathEndMode.Touch, Danger.Deadly);
                bool reservable = pawn.CanReserve(colonist, 1, -1, null, false);
                if (RHAH_Begging.CanReceive(pawn, colonist, reachable, reservable))
                {
                    return colonist;
                }
            }

            return null;
        }

        static void Insert(Verse.Pawn[] picked, float[] scores, ref int count, Verse.Pawn colonist, float distance)
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

            picked[index] = colonist;
            scores[index] = distance;
            if (count < PathChecks)
            {
                count++;
            }
        }
    }
}
