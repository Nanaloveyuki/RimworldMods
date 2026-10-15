using System.Collections.Generic;
using HungerAndHavoc.Api;
using HungerAndHavoc.Core;
using RimWorld;
using Verse;
using Verse.AI;

namespace HungerAndHavoc.Pawn
{
    public class JobDriver_RHAH_Gnaw : JobDriver
    {
        const int GnawTicks = 180;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.WaitWith(TargetIndex.A, GnawTicks, true);
            Toil finish = new Toil();
            finish.initAction = delegate
            {
                Thing target = job.GetTarget(TargetIndex.A).Thing;
                bool wall = target != null && target.def.plant == null;
                GnawBite bite = RHAH_GnawHealth.ForTarget(wall);
                RHAH_GnawHealth.DamageTarget(target, bite.Damage, pawn);

                Need_Food food = pawn.needs != null ? pawn.needs.food : null;
                if (food != null)
                {
                    food.CurLevel += bite.Nutrition;
                }

                HediffDef wound = wall
                    ? RHAH_DefOf.RHAH_GnawedWall
                    : RHAH_DefOf.RHAH_GnawedBark;
                Refresh(pawn, wound, bite.Severity);
                if (!wall)
                {
                    AddToxic(pawn, bite.Toxic);
                }
                else
                {
                    MapComponent_RHAH_Map mapState = pawn.Map != null
                        ? pawn.Map.GetComponent<MapComponent_RHAH_Map>()
                        : null;
                    int count = mapState != null ? mapState.NextWallGnaw(pawn.thingIDNumber) : 0;
                    if (RHAH_GnawHealth.IsOvergnaw(count))
                    {
                        Refresh(pawn, RHAH_DefOf.RHAH_OvergnawedWall, RHAH_GnawHealth.OverSeverity);
                        if (food != null)
                        {
                            food.CurLevel = RHAH_GnawHealth.ClampFood(food.CurLevel, food.MaxLevel);
                        }
                    }
                }
                // 啃食只补一点营养 不记成吃饱离场
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }

        static void Refresh(Verse.Pawn pawn, HediffDef def, float severity)
        {
            if (pawn.health == null || def == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(def, pawn);
                hediff.Severity = severity;
                pawn.health.AddHediff(hediff);
            }
            else if (hediff.Severity < severity)
            {
                hediff.Severity = severity;
            }

            HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears == null)
            {
                return;
            }

            disappears.disappearsAfterTicks = RHAH_GnawHealth.DurationTicks;
            disappears.ticksToDisappear = RHAH_GnawHealth.DurationTicks;
        }

        static void AddToxic(Verse.Pawn pawn, float amount)
        {
            if (pawn.health == null || amount <= 0f || HediffDefOf.ToxicBuildup == null)
            {
                return;
            }

            Hediff toxic = pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.ToxicBuildup);
            if (toxic == null)
            {
                toxic = HediffMaker.MakeHediff(HediffDefOf.ToxicBuildup, pawn);
                toxic.Severity = amount;
                pawn.health.AddHediff(toxic);
                return;
            }

            toxic.Severity += amount;
        }
    }
}
