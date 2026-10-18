using System.Collections.Generic;
using HungerAndHavoc.Core;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobDriver_RHAH_Beg : JobDriver
    {
        const int BegTicks = 150;

        Verse.Pawn TargetPawn
        {
            get
            {
                return job.GetTarget(TargetIndex.A).Pawn;
            }
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            Verse.Pawn target = TargetPawn;
            bool reachable = target != null && pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly);
            bool reservable = target != null && pawn.CanReserve(target, 1, -1, null, false);
            if (!RHAH_Begging.CanReceive(pawn, target, reachable, reservable))
            {
                return false;
            }

            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, false);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOnDowned(TargetIndex.A);
            this.FailOn(() => !StillReceives(TargetPawn));
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, BegTicks, true, false, false, TargetIndex.A);
            Toil resolve = ToilMaker.MakeToil("ResolveBegging");
            resolve.initAction = Resolve;
            resolve.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return resolve;
        }

        bool StillReceives(Verse.Pawn target)
        {
            return RHAH_Begging.CanReceive(pawn, target, true, true);
        }

        void Resolve()
        {
            Verse.Pawn target = TargetPawn;
            float social = 0f;
            if (pawn.skills != null)
            {
                SkillRecord skill = pawn.skills.GetSkill(SkillDefOf.Social);
                if (skill != null)
                {
                    social = skill.Level;
                }
            }

            float chance = RHAH_VisitorRules.BegSuccessChance(
                RHAH_Begging.SuccessChancePercent(),
                (int)social,
                RHAH_Begging.SocialBonusPercent());
            bool repeat = RHAH_Begging.HasBegged(pawn, target);
            RHAH_Begging.TrySlap(pawn, target);
            RHAH_Begging.RememberBeg(pawn, target);
            if (pawn.stances != null && pawn.stances.stunner != null && pawn.stances.stunner.Stunned)
            {
                return;
            }

            bool success = !repeat && Rand.Chance(chance) && RHAH_Begging.TryTakeFood(pawn, target);
            if (success)
            {
                RHAH_Begging.NoteSuccess(pawn, target);
                return;
            }

            RHAH_Begging.NoteFailure(pawn, target);
            Verse.Pawn next = NextColonist(target);
            if (next != null)
            {
                Job again = JobMaker.MakeJob(RHAH_DefOf.RHAH_Beg, next);
                pawn.jobs.StartJob(again, JobCondition.Succeeded, null, false, true, null, null, false, false, null, false, true, false);
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            RHAH_Begging.StartFailCooldown(pawn, now, RHAH_Begging.CooldownHours());
            Job wander = JobMaker.MakeJob(JobDefOf.GotoWander, pawn.Position);

            pawn.jobs.StartJob(wander, JobCondition.Succeeded, null, false, true, null, null, false, false, null, false, true, false);
        }

        Verse.Pawn NextColonist(Verse.Pawn skipped)
        {
            if (pawn.Map == null || RHAH_Begging.PrefersGnaw(pawn))
            {
                return null;
            }

            Verse.Pawn best = null;
            float bestDist = float.MaxValue;
            foreach (Verse.Pawn colonist in pawn.Map.mapPawns.FreeColonistsSpawned)
            {
                if (colonist == null || colonist == pawn || colonist == skipped)
                {
                    continue;
                }

                bool reachable = pawn.CanReach(colonist, PathEndMode.Touch, Danger.Deadly);
                bool reservable = pawn.CanReserve(colonist, 1, -1, null, false);
                if (!RHAH_Begging.CanReceive(pawn, colonist, reachable, reservable))
                {
                    continue;
                }

                float dist = colonist.Position.DistanceToSquared(pawn.Position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = colonist;
                }
            }

            return best;
        }
    }
}
