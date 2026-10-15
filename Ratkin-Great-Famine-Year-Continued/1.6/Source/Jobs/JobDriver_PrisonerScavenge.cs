using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MouseDisaster
{
    public class JobDriver_PrisonerScavenge : JobDriver
    {
        private const int ScavengeTicks = 150;

        private Filth TargetFilth => job.GetTarget(TargetIndex.A).Thing as Filth;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(TargetFilth, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.OnCell);

            Toil scavenge = Toils_General.WaitWith(TargetIndex.A, ScavengeTicks, true);
            scavenge.initAction = ApplyCrawlEatingAnimation;
            scavenge.tickAction = ApplyCrawlEatingAnimation;
            scavenge.AddFinishAction(ClearCrawlEatingAnimation);
            scavenge.WithProgressBar(TargetIndex.A, () => 1f - (float)pawn.jobs.curDriver.ticksLeftThisToil / ScavengeTicks, interpolateBetweenActorAndTarget: true);
            scavenge.PlaySustainerOrSound(() => SoundDefOf.Interact_CleanFilth);
            yield return scavenge;

            Toil finish = ToilMaker.MakeToil("PrisonerScavengeFinish");
            finish.initAction = delegate
            {
                Filth filth = TargetFilth;
                if (filth == null)
                {
                    return;
                }

                MouseDisasterUtility.PrisonerScavengeProfile profile = MouseDisasterUtility.ResolvePrisonerScavengeProfile(filth);
                filth.ThinFilth();
                MouseDisasterUtility.AddNutrition(pawn, profile.nutritionGain);
                bool poisoned = ApplyPoisoning();
                ApplyMood(profile);
                ShowResultText(poisoned, profile);
                MouseDisasterUtility.NotifyPrisonerScavengeCompleted(pawn);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        private bool ApplyPoisoning()
        {
            return MouseDisasterUtility.TryApplyPrisonerScavengePoison(pawn);
        }

        private void ApplyMood(MouseDisasterUtility.PrisonerScavengeProfile profile)
        {
            MouseDisasterUtility.ApplyPrisonerScavengeMood(pawn, profile?.thoughtDef);
        }

        private void ShowResultText(bool poisoned, MouseDisasterUtility.PrisonerScavengeProfile profile)
        {
            if (!pawn.Spawned || pawn.Map == null)
            {
                return;
            }

            string text = MouseDisasterUtility.RandomText(profile?.textKeys);
            if (poisoned)
            {
                text = text + "\n" + MouseDisasterUtility.RandomPrisonerScavengePoisonedText();
            }

            MouseDisasterUtility.TryThrowText(pawn.Map, pawn.DrawPos, text, 3.5f);
        }

        private void ApplyCrawlEatingAnimation()
        {
            MouseDisasterUtility.SetPrisonerScavengeCrawlAnimation(pawn);
        }

        private void ClearCrawlEatingAnimation()
        {
            MouseDisasterUtility.ClearPrisonerScavengeCrawlAnimation(pawn);
        }
    }
}
