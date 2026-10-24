using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// All piece-rate settlement hooks (see <see cref="PieceRateWorker"/> for the unit mapping):
    /// production bills, mining, construction, growing/plant-cutting, cleaning, hauling, research,
    /// plus the generic per-job fallback for unknown work types.
    /// All hooks share work attribution; job completion captures it before pooled cleanup.
    /// </summary>
    public static class Harmony_PieceRate
    {
        private static void CreditCurrent(Pawn pawn, WorkUnit unit)
        {
            var work = WorkAttribution.Resolve(pawn, pawn.jobs?.curJob);
            if (WorkBilling.IsKnown(work) && WorkBilling.UsesUnit(work, unit))
                PieceRateWorker.Credit(pawn, work, 1f);
        }

        /// <summary>Production tables: one completed recipe batch.</summary>
        [HarmonyPatch(typeof(RecordsUtility), nameof(RecordsUtility.Notify_BillDone))]
        public static class Patch_BillDone
        {
            [HarmonyPostfix]
            static void Postfix(Pawn billDoer)
            {
                if (billDoer == null || !billDoer.IsPrisonerOfColony) return;
                CreditCurrent(billDoer, WorkUnit.Bill);
            }
        }

        /// <summary>Mining: one mined deposit.</summary>
        [HarmonyPatch(typeof(Mineable), nameof(Mineable.DestroyMined))]
        public static class Patch_Mineable
        {
            [HarmonyPostfix]
            static void Postfix(Pawn pawn)
            {
                if (pawn == null || !pawn.IsPrisonerOfColony) return;
                CreditCurrent(pawn, WorkUnit.Mining);
            }
        }

        /// <summary>Construction: one finished building / floor.</summary>
        [HarmonyPatch(typeof(Frame), nameof(Frame.CompleteConstruction))]
        public static class Patch_Frame
        {
            [HarmonyPostfix]
            static void Postfix(Pawn worker)
            {
                if (worker == null || !worker.IsPrisonerOfColony) return;
                CreditCurrent(worker, WorkUnit.Construction);
            }
        }

        /// <summary>Growing harvest + PlantCutting: one harvested / cut plant.</summary>
        [HarmonyPatch(typeof(Plant), nameof(Plant.PlantCollected))]
        public static class Patch_Plant
        {
            [HarmonyPostfix]
            static void Postfix(Pawn by)
            {
                if (by == null || !by.IsPrisonerOfColony) return;
                CreditCurrent(by, WorkUnit.Plant);
            }
        }

        /// <summary>
        /// Sowing (PlantsSown) and Cleaning (MessesCleaned). These jobs process target queues, so the
        /// record is the only exact "one unit done" event; rain-washed filth never touches the record.
        /// </summary>
        [HarmonyPatch(typeof(Pawn_RecordsTracker), nameof(Pawn_RecordsTracker.Increment), new Type[] { typeof(RecordDef) })]
        public static class Patch_Records
        {
            [HarmonyPostfix]
            static void Postfix(Pawn_RecordsTracker __instance, RecordDef def)
            {
                if (def != RecordDefOf.PlantsSown && def != RecordDefOf.MessesCleaned) return;
                var pawn = __instance?.pawn;
                if (pawn == null || !pawn.IsPrisonerOfColony) return;
                CreditCurrent(pawn, def == RecordDefOf.PlantsSown ? WorkUnit.Sowing : WorkUnit.Cleaning);
            }
        }

        /// <summary>
        /// Hauling: one carried stack placed on the map (floor/shelf) with ThingPlaceMode.Direct
        /// while executing a Hauling job, and only when placement succeeds. Cleanup drops use
        /// Near and bill ingredient drops are excluded by the work-type check. Container transfers
        /// use ThingOwner.TryTransferToContainer, not this preexisting per-stack settlement hook.
        /// TargetMethod selects the exact overload including its out parameter.
        /// </summary>
        [HarmonyPatch]
        public static class Patch_CarryDrop
        {
            static System.Reflection.MethodBase TargetMethod()
            {
                return AccessTools.Method(typeof(Pawn_CarryTracker), nameof(Pawn_CarryTracker.TryDropCarriedThing),
                    new Type[] { typeof(IntVec3), typeof(ThingPlaceMode), typeof(Thing).MakeByRefType(), typeof(Action<Thing, int>) });
            }

            [HarmonyPostfix]
            static void Postfix(Pawn_CarryTracker __instance, ThingPlaceMode mode, bool __result)
            {
                if (!__result || mode != ThingPlaceMode.Direct) return;
                var pawn = __instance?.pawn;
                if (pawn == null || !pawn.IsPrisonerOfColony) return;
                CreditCurrent(pawn, WorkUnit.Hauling);
            }
        }

        /// <summary>Research: split the per-tech wage among contributors when a project completes.</summary>
        [HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
        public static class Patch_Research
        {
            [HarmonyPostfix]
            static void Postfix(ResearchProjectDef proj)
            {
                PieceRateWorker.DistributeResearch(proj);
            }
        }

        /// <summary>
        /// Generic fallback: snapshot attribution before 1.7 cleanup clears the driver and pools the job.
        /// </summary>
        [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
        public static class Patch_JobDone
        {
            internal readonly struct Completion
            {
                internal readonly Pawn Pawn;
                internal readonly AttributedWork Work;

                internal Completion(Pawn pawn, AttributedWork work)
                {
                    Pawn = pawn;
                    Work = work;
                }
            }

            [HarmonyPrefix]
            static void Prefix(Pawn_JobTracker __instance, JobCondition condition, out Completion __state)
            {
                __state = default;
                if (condition != JobCondition.Succeeded) return;
                var driver = __instance.curDriver;
                var pawn = driver?.pawn;
                if (pawn == null || !pawn.IsPrisonerOfColony) return;
                var work = WorkAttribution.Resolve(pawn, __instance.curJob);
                if (!WorkBilling.IsKnown(work) || !WorkBilling.UsesUnit(work, WorkUnit.Job) || !work.CanComplete(driver)) return;
                __state = new Completion(pawn, work);
            }

            [HarmonyPostfix]
            static void Postfix(Completion __state)
            {
                if (__state.Pawn != null) PieceRateWorker.Credit(__state.Pawn, __state.Work, 1f);
            }
        }
    }
}
