using System.Collections.Generic;
using RimWorld;
using Verse;

namespace HungerAndHavoc.Pawn.Compat
{
    // 只读生命阶段和公开 Hediff 阶段 不反射 Toddlers
    internal static class RHAH_ChildMovement
    {
        internal const string LearningToWalk = "LearningToWalk";
        internal const string LearningManipulation = "LearningManipulation";

        internal static bool IsVanillaImmobileBaby(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.DevelopmentalStage != DevelopmentalStage.Baby)
            {
                return false;
            }

            if (LiftsAgeImmobility(pawn))
            {
                return false;
            }

            LifeStageDef stage = pawn.ageTracker != null ? pawn.ageTracker.CurLifeStage : null;
            return stage == null || stage.alwaysDowned;
        }

        internal static bool LiftsAgeImmobility(Verse.Pawn pawn)
        {
            bool toddlers = ModsConfig.IsActive("cyanobot.toddlers");
            bool eventBaby = HungerAndHavoc.Api.RHAH_Api.IsOrigin(pawn) && pawn.DevelopmentalStage == DevelopmentalStage.Baby;
            bool injured = pawn.health != null && pawn.health.Downed && !AgeOnlyDown(pawn);
            LifeStageDef stage = pawn.ageTracker != null ? pawn.ageTracker.CurLifeStage : null;
            bool ageDowned = stage == null || stage.alwaysDowned;
            return RHAH_VisitorRules.LiftsAgeImmobility(toddlers, eventBaby, ageDowned, injured);
        }

        static bool AgeOnlyDown(Verse.Pawn pawn)
        {
            LifeStageDef stage = pawn.ageTracker != null ? pawn.ageTracker.CurLifeStage : null;
            return stage == null || stage.alwaysDowned;
        }

        internal static bool CanOpenDoors(Verse.Pawn pawn)
        {
            if (IsVanillaImmobileBaby(pawn))
            {
                return false;
            }

            Hediff walk = First(pawn, LearningToWalk);
            return walk == null || walk.CurStageIndex > 0;
        }

        // 0 不能自己吃 1 可以坐在地上吃 2 起要正常站格
        internal static int ManipulationStage(Verse.Pawn pawn)
        {
            if (IsVanillaImmobileBaby(pawn))
            {
                return 0;
            }

            Hediff manipulation = First(pawn, LearningManipulation);
            if (manipulation == null)
            {
                return 2;
            }

            return manipulation.CurStageIndex;
        }

        internal static bool CanFeedSelf(Verse.Pawn pawn)
        {
            return ManipulationStage(pawn) >= 1;
        }

        internal static bool EatsOnFloor(Verse.Pawn pawn)
        {
            return ManipulationStage(pawn) == 1;
        }

        internal static bool CanWalkOut(Verse.Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Downed || pawn.CarriedBy != null)
            {
                return false;
            }

            if (IsVanillaImmobileBaby(pawn))
            {
                return false;
            }

            PawnCapacitiesHandler capacities = pawn.health != null ? pawn.health.capacities : null;
            if (capacities != null && !capacities.CapableOf(PawnCapacityDefOf.Moving))
            {
                return false;
            }

            return CanOpenDoors(pawn);
        }

        static Hediff First(Verse.Pawn pawn, string defName)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff != null && hediff.def != null && hediff.def.defName == defName)
                {
                    return hediff;
                }
            }

            return null;
        }
    }
}
