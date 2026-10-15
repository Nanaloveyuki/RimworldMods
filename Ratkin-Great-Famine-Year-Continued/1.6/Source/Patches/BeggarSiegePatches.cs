using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    public static class BeggarSiegeJobCompletionPatch
    {
        private static readonly AccessTools.FieldRef<Pawn_JobTracker, Pawn> PawnField =
            AccessTools.FieldRefAccess<Pawn_JobTracker, Pawn>("pawn");

        private struct JobEndState
        {
            public Pawn pawn;
            public Job job;
        }

        private static void Prefix(Pawn_JobTracker __instance, out JobEndState __state)
        {
            __state = new JobEndState
            {
                pawn = PawnField(__instance),
                job = __instance?.curJob
            };
        }

        private static void Postfix(JobCondition condition, JobEndState __state)
        {
            if (condition != JobCondition.Succeeded || __state.pawn == null || __state.job == null)
            {
                return;
            }

            if (!MouseDisasterUtility.IsSiegeBeggar(__state.pawn))
            {
                return;
            }

            JobDef def = __state.job.def;
            if (def == JobDefOf.Goto && __state.job.exitMapOnArrival)
            {
                MouseDisasterUtility.ResetBeggarState(__state.pawn);
                return;
            }

            if (def == MouseDisasterDefOf.MouseDisaster_BegForFood)
            {
                return;
            }

            if (def == JobDefOf.TakeInventory || def == JobDefOf.TakeFromOtherInventory || def == JobDefOf.Ingest)
            {
                MouseDisasterUtility.MarkSiegeBeggarStoleFood(__state.pawn);
            }
        }
    }
}
