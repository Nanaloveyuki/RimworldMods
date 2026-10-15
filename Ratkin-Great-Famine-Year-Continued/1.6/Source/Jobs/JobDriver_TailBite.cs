using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobDriver_TailBite : JobDriver
    {
        private const int BiteTicks = 150;

        private Pawn TargetPawn => (Pawn)job.targetA.Thing;

        private BodyPartRecord TailPart => MouseDisasterUtility.GetNaturalRatTail(TargetPawn);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetPawn, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedOrNull(TargetIndex.A);
            this.FailOn(() => !MouseDisasterUtility.CanBeTailBiteTarget(TargetPawn, pawn));

            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);

            Toil bite = Toils_General.WaitWith(TargetIndex.A, BiteTicks, true);
            bite.WithProgressBar(TargetIndex.A, () => 1f - (float)pawn.jobs.curDriver.ticksLeftThisToil / BiteTicks, interpolateBetweenActorAndTarget: true);
            bite.initAction = delegate
            {
                MouseDisasterUtility.TryThrowTextThrottled(pawn.Map, pawn.DrawPos, MouseDisasterUtility.RandomText(new[]
                {
                    "MouseDisaster_TailBite_Text_Start_1",
                    "MouseDisaster_TailBite_Text_Start_2",
                    "MouseDisaster_TailBite_Text_Start_3"
                }), "tailbite_start_" + pawn.thingIDNumber, MouseDisasterUtility.TailBiteStartTextCooldown, 2.5f);
            };
            yield return bite;

            Toil finish = ToilMaker.MakeToil("TailBiteFinish");
            finish.initAction = delegate
            {
                Pawn target = TargetPawn;
                BodyPartRecord tail = TailPart;
                if (target == null || tail == null)
                {
                    return;
                }

                MouseDisasterUtility.RecordTailBiteVictim(target);

                bool defenseless = MouseDisasterUtility.IsDefenselessTailTarget(target);
                bool success = defenseless || Rand.Chance(0.3f);

                if (success)
                {
                    BiteOffTail(target, tail);
                    MouseDisasterUtility.TrySpawnRatEggTailDrop(target);
                    MouseDisasterUtility.AddNutrition(pawn, 0.8f);
                    GainTailThoughts(success: true, target);
                    MouseDisasterUtility.NotifyTailBiteOffEvent(pawn, target);
                    MouseDisasterUtility.TryThrowTextThrottled(pawn.Map, pawn.DrawPos, MouseDisasterUtility.RandomText(new[]
                    {
                        "MouseDisaster_TailBite_Text_Success_1",
                        "MouseDisaster_TailBite_Text_Success_2",
                        "MouseDisaster_TailBite_Text_Success_3"
                    }), "tailbite_success_" + target.thingIDNumber, MouseDisasterUtility.TailBiteResultTextCooldown, 2.5f);
                }
                else
                {
                    BiteInjureTail(target, tail);
                    GainTailThoughts(success: false, target);
                    MouseDisasterUtility.TryThrowTextThrottled(target.Map, target.DrawPos, MouseDisasterUtility.RandomText(new[]
                    {
                        "MouseDisaster_TailBite_Text_Failed_1",
                        "MouseDisaster_TailBite_Text_Failed_2",
                        "MouseDisaster_TailBite_Text_Failed_3"
                    }), "tailbite_fail_" + target.thingIDNumber, MouseDisasterUtility.TailBiteResultTextCooldown, 2.5f);
                }

                TryStartSocialFight(pawn, target, success);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private void BiteOffTail(Pawn target, BodyPartRecord tail)
        {
            if (target.health.hediffSet.PartIsMissing(tail))
            {
                return;
            }

            Hediff hediff = target.health.GetOrAddHediff(HediffDefOf.MissingBodyPart, tail);
            if (hediff is Hediff_MissingPart missing)
            {
                missing.IsFresh = true;
                missing.lastInjury = DamageDefOf.Bite.hediff;
            }
        }

        private void BiteInjureTail(Pawn target, BodyPartRecord tail)
        {
            DamageInfo dinfo = new DamageInfo(DamageDefOf.Bite, 5f, 0f, -1f, pawn, tail);
            target.TakeDamage(dinfo);
        }

        private void GainTailThoughts(bool success, Pawn target)
        {
            if (pawn.needs?.mood?.thoughts?.memories != null)
            {
                ThoughtDef def = success ? MouseDisasterDefOf.MouseDisaster_TailBiteSuccess : MouseDisasterDefOf.MouseDisaster_TailBiteFailed;
                pawn.needs.mood.thoughts.memories.TryGainMemory(def, target);
            }

            if (target.needs?.mood?.thoughts?.memories != null)
            {
                ThoughtDef def2 = success ? MouseDisasterDefOf.MouseDisaster_TailBitten : MouseDisasterDefOf.MouseDisaster_TailBittenInjured;
                target.needs.mood.thoughts.memories.TryGainMemory(def2, pawn);
                if (success)
                {
                    MouseDisasterUtility.TryGainTailBittenHatredMemory(target, pawn);
                }
            }
        }

        private void TryStartSocialFight(Pawn initiator, Pawn other, bool success)
        {
            if (initiator.Dead || other.Dead || !initiator.Spawned || !other.Spawned)
            {
                return;
            }

            if (initiator.DevelopmentalStage != DevelopmentalStage.Adult || other.DevelopmentalStage != DevelopmentalStage.Adult)
            {
                return;
            }

            if (MouseDisasterUtility.IsDefenselessTailTarget(other))
            {
                return;
            }

            initiator.interactions?.StartSocialFight(other);
        }
    }
}
