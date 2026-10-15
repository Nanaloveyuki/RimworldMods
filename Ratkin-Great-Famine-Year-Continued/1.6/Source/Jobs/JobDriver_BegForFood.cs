using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobDriver_BegForFood : JobDriver
    {
        private const int BegTicks = 150;

        private Pawn TargetPawn => job.GetTarget(TargetIndex.A).Pawn;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (pawn.DevelopmentalStage == DevelopmentalStage.Baby ||
                !MouseDisasterUtility.CanReceiveBegging(TargetPawn) ||
                !MouseDisasterUtility.CanBegAgain(pawn) || !MouseDisasterUtility.CanBegAgain(TargetPawn) ||
                !pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed)) return false;
            MouseDisasterUtility.StartBeggingCooldown(pawn, TargetPawn);
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => !MouseDisasterUtility.CanReceiveBegging(TargetPawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, BegTicks, true);
            Toil resolve = ToilMaker.MakeToil("ResolveBegging");
            resolve.initAction = delegate
            {
                Pawn target = TargetPawn;
                string targetLabel = target?.LabelShort ?? "...";
                float social = pawn.skills?.GetSkill(SkillDefOf.Social)?.Level ?? 0f;
                float chance = Mathf.Clamp01(0.35f + social * 0.03f);
                bool success = Rand.Chance(chance) && MouseDisasterUtility.TryConsumeBeggedFood(pawn, target);
                MouseDisasterUtility.RecordBegAttempt(pawn, target, success);
                MouseDisasterUtility.RecordBeggarInteractionLog(pawn, target, success);
                MouseDisasterUtility.ApplyBeggarWitnessThought(target);
                if (success)
                {
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(MouseDisasterDefOf.MouseDisaster_BeggingSucceeded, target);
                    if (pawn.Spawned)
                    {
                        MouseDisasterUtility.TryThrowText(pawn.Map, pawn.DrawPos, MouseDisasterUtility.RandomText(new[]
                        {
                            "MouseDisaster_Beg_Text_Success_1",
                            "MouseDisaster_Beg_Text_Success_2",
                            "MouseDisaster_Beg_Text_Success_3"
                        }), 3f);
                    }
                }
                else if (pawn.Spawned)
                {
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(MouseDisasterDefOf.MouseDisaster_BeggingRejected, target);
                    MouseDisasterUtility.TryThrowText(pawn.Map, pawn.DrawPos, MouseDisasterUtility.RandomText(new[]
                    {
                        "MouseDisaster_Beg_Text_Fail_1",
                        "MouseDisaster_Beg_Text_Fail_2",
                        "MouseDisaster_Beg_Text_Fail_3"
                    }, targetLabel), 3f);
                }
            };
            resolve.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return resolve;
        }
    }
}
