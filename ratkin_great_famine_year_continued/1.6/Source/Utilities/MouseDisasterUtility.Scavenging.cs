using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MouseDisaster
{
    public static partial class MouseDisasterUtility
    {

        public static Job TryCreatePrisonerScavengeJob(Pawn pawn, bool logDecision)
        {
            if (!CanPrisonerScavenge(pawn))
            {
                if (pawn != null)
                {
                    ClearPrisonerScavengeState(pawn);
                }

                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: not a valid prisoner baby on map");
                }

                return null;
            }

            if (pawn.needs.food.CurCategory < HungerCategory.Hungry)
            {
                ResetPrisonerScavengeDelayStateIfNotHungry(pawn, logDecision);

                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: hunger below Hungry");
                }

                return null;
            }

            if (IsPrisonerScavengeBurstOnCooldown(pawn))
            {
                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: burst cooldown active");
                }

                return null;
            }

            bool burstActive = IsPrisonerScavengeBurstActive(pawn);

            if (ShouldDeferPrisonerScavengeForCare(pawn, logDecision))
            {
                return null;
            }

            if (logDecision)
            {
                LogPrisonerScavenge(pawn, "pass: colony captive mode allows scavenge despite available food");
            }

            Filth filth = FindScavengeableFilth(pawn);
            if (filth == null)
            {
                if (burstActive)
                {
                    EndPrisonerScavengeBurst(pawn, enterCooldown: false, logDecision, "reset: burst ended because no filth target");
                }

                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: no scavengeable filth in range");
                }

                return null;
            }

            if (!burstActive)
            {
                StartPrisonerScavengeBurst(pawn, logDecision);
            }

            if (logDecision)
            {
                int remaining = GetPrisonerScavengeBurstRemaining(pawn);
                LogPrisonerScavenge(pawn, "pass: target=" + filth.def.defName + " at " + filth.Position + ", burstRemaining=" + remaining);
            }

            return JobMaker.MakeJob(MouseDisasterDefOf.MouseDisaster_PrisonerScavenge, filth);
        }

        private static int GetPrisonerScavengeBurstRemaining(Pawn pawn)
        {
            if (pawn == null || !PrisonerScavengeBurstRemainingByPawnId.TryGetValue(pawn.thingIDNumber, out int remaining))
            {
                return 0;
            }

            return Mathf.Max(0, remaining);
        }

        private static void ClearPrisonerScavengeState(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            int pawnId = pawn.thingIDNumber;
            PrisonerScavengeDelayStateByPawnId.Remove(pawnId);
            PrisonerScavengeBurstRemainingByPawnId.Remove(pawnId);
            PrisonerScavengeBurstCooldownByPawnId.Remove(pawnId);
        }

        private static bool IsPrisonerScavengeBurstActive(Pawn pawn)
        {
            return GetPrisonerScavengeBurstRemaining(pawn) > 0;
        }

        private static bool IsPrisonerScavengeBurstOnCooldown(Pawn pawn)
        {
            if (pawn == null || !PrisonerScavengeBurstCooldownByPawnId.TryGetValue(pawn.thingIDNumber, out int untilTick) || untilTick <= 0)
            {
                return false;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (untilTick > nowTick)
            {
                return true;
            }

            PrisonerScavengeBurstCooldownByPawnId.Remove(pawn.thingIDNumber);
            return false;
        }

        private static void StartPrisonerScavengeBurst(Pawn pawn, bool logDecision)
        {
            if (pawn == null)
            {
                return;
            }

            int remaining = Rand.RangeInclusive(PrisonerScavengeBurstMinCount, PrisonerScavengeBurstMaxCount);
            PrisonerScavengeBurstRemainingByPawnId[pawn.thingIDNumber] = remaining;
            PrisonerScavengeBurstCooldownByPawnId.Remove(pawn.thingIDNumber);
            if (logDecision)
            {
                LogPrisonerScavenge(pawn, "start: burst initialized with remaining=" + remaining);
            }
        }

        private static void EndPrisonerScavengeBurst(Pawn pawn, bool enterCooldown, bool logDecision, string reason)
        {
            if (pawn == null)
            {
                return;
            }

            bool hadState = PrisonerScavengeBurstRemainingByPawnId.Remove(pawn.thingIDNumber);
            if (enterCooldown)
            {
                int nowTick = Find.TickManager?.TicksGame ?? 0;
                PrisonerScavengeBurstCooldownByPawnId[pawn.thingIDNumber] = nowTick + PrisonerScavengeBurstCooldownTicks;
            }
            else
            {
                PrisonerScavengeBurstCooldownByPawnId.Remove(pawn.thingIDNumber);
            }

            if (logDecision && hadState)
            {
                LogPrisonerScavenge(pawn, reason);
            }
        }

        public static void NotifyPrisonerScavengeCompleted(Pawn pawn)
        {
            if (pawn == null || !PrisonerScavengeBurstRemainingByPawnId.TryGetValue(pawn.thingIDNumber, out int remaining) || remaining <= 0)
            {
                return;
            }

            remaining--;
            if (remaining > 0)
            {
                PrisonerScavengeBurstRemainingByPawnId[pawn.thingIDNumber] = remaining;
                if (IsPrisonerScavengeDebugLogEnabled)
                {
                    LogPrisonerScavenge(pawn, "progress: burst remaining=" + remaining);
                }

                return;
            }

            EndPrisonerScavengeBurst(pawn, enterCooldown: true, IsPrisonerScavengeDebugLogEnabled, "finish: burst completed and cooldown started");
        }

        public static bool IsPrisonerScavengeDeferred(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            ResetPrisonerScavengeDelayStateIfNotHungry(pawn, logDecision: false);
            if (!PrisonerScavengeDelayStateByPawnId.TryGetValue(pawn.thingIDNumber, out int stateTick) || stateTick <= 0)
            {
                return false;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (stateTick > nowTick)
            {
                return true;
            }

            PrisonerScavengeDelayStateByPawnId[pawn.thingIDNumber] = 0;
            return false;
        }

        public static void ResetPrisonerScavengeDelayStateIfNotHungry(Pawn pawn, bool logDecision)
        {
            if (pawn?.needs?.food == null)
            {
                return;
            }

            if (pawn.needs.food.CurCategory >= HungerCategory.Hungry)
            {
                return;
            }

            if (PrisonerScavengeDelayStateByPawnId.Remove(pawn.thingIDNumber) && logDecision)
            {
                LogPrisonerScavenge(pawn, "reset: delay state cleared after feeding");
            }

            EndPrisonerScavengeBurst(pawn, enterCooldown: false, logDecision, "reset: burst ended after feeding");
        }

        private static bool ShouldDeferPrisonerScavengeForCare(Pawn pawn, bool logDecision)
        {
            if (pawn == null || pawn.DevelopmentalStage != DevelopmentalStage.Baby)
            {
                return false;
            }

            int pawnId = pawn.thingIDNumber;
            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (PrisonerScavengeDelayStateByPawnId.TryGetValue(pawnId, out int stateTick))
            {
                if (stateTick > nowTick)
                {
                    if (logDecision)
                    {
                        int remain = stateTick - nowTick;
                        LogPrisonerScavenge(pawn, "skip: waiting colony care feed window (" + remain + " ticks left)");
                    }

                    return true;
                }

                if (stateTick > 0)
                {
                    PrisonerScavengeDelayStateByPawnId[pawnId] = 0;
                    if (logDecision)
                    {
                        LogPrisonerScavenge(pawn, "pass: colony care delay window elapsed");
                    }
                }

                return false;
            }

            if (!HasPendingPrisonerBabyCare(pawn))
            {
                return false;
            }

            int deferUntilTick = nowTick + PrisonerScavengeCareDelayTicks;
            PrisonerScavengeDelayStateByPawnId[pawnId] = deferUntilTick;
            if (logDecision)
            {
                LogPrisonerScavenge(pawn, "skip: colony care detected, defer scavenging for 3h");
            }

            return true;
        }

        private static bool HasPendingPrisonerBabyCare(Pawn pawn)
        {
            if (pawn?.Map == null || pawn.needs?.food == null)
            {
                return false;
            }

            if (!FoodUtility.ShouldBeFedBySomeone(pawn))
            {
                return false;
            }

            List<Pawn> feeders = pawn.Map.mapPawns?.FreeColonistsSpawned;
            if (feeders == null || feeders.Count == 0)
            {
                return false;
            }

            bool desperate = pawn.needs.food.CurCategory == HungerCategory.Starving;
            for (int i = 0; i < feeders.Count; i++)
            {
                Pawn feeder = feeders[i];
                if (!IsPotentialPrisonerBabyFeeder(feeder, pawn))
                {
                    continue;
                }

                Job curJob = feeder.CurJob;
                if (curJob?.def == JobDefOf.FeedPatient && curJob.targetB.Thing == pawn)
                {
                    return true;
                }

                if (FoodUtility.TryFindBestFoodSourceFor(feeder, pawn, desperate, out Thing _, out ThingDef _, canRefillDispenser: false, canUseInventory: true, canUsePackAnimalInventory: false, allowForbidden: false, allowCorpse: false))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsPotentialPrisonerBabyFeeder(Pawn feeder, Pawn baby)
        {
            if (feeder == null || baby == null || feeder == baby || feeder.Map != baby.Map || feeder.Dead || feeder.Downed || feeder.Drafted || feeder.InMentalState || !feeder.Awake())
            {
                return false;
            }

            if (!feeder.RaceProps.Humanlike)
            {
                return false;
            }

            if (!feeder.CanReserveAndReach(baby, PathEndMode.Touch, Danger.Some, 1, -1, null, ignoreOtherReservations: true))
            {
                return false;
            }

            bool childcareActive = IsWorkTypeActive(feeder, WorkTypeDefOf.Childcare);
            bool wardenActive = IsWorkTypeActive(feeder, WorkTypeDefOf.Warden);
            return childcareActive || wardenActive;
        }

        private static bool IsWorkTypeActive(Pawn pawn, WorkTypeDef workType)
        {
            return pawn?.workSettings != null && workType != null && !pawn.WorkTypeIsDisabled(workType) && pawn.workSettings.WorkIsActive(workType);
        }

        public static void SetPrisonerScavengeCrawlAnimation(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer == null)
            {
                return;
            }

            AnimationDef animation = ResolveToddlerCrawlAnimation();
            if (animation != null)
            {
                pawn.Drawer.renderer.SetAnimation(animation);
            }
        }

        public static void ClearPrisonerScavengeCrawlAnimation(Pawn pawn)
        {
            if (pawn?.Drawer?.renderer == null)
            {
                return;
            }

            pawn.Drawer.renderer.SetAnimation(null);
        }

        private static AnimationDef ResolveToddlerCrawlAnimation()
        {
            if (toddlerCrawlAnimationResolved)
            {
                return toddlerCrawlAnimationDef;
            }

            toddlerCrawlAnimationResolved = true;
            toddlerCrawlAnimationDef = DefDatabase<AnimationDef>.GetNamedSilentFail("ToddlerCrawl");
            return toddlerCrawlAnimationDef;
        }

        public static bool TryStartPrisonerScavengeJob(Pawn pawn, bool logDecision)
        {
            if (pawn?.jobs == null)
            {
                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: no job tracker");
                }

                return false;
            }

            if (pawn.CurJobDef == MouseDisasterDefOf.MouseDisaster_PrisonerScavenge)
            {
                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: already scavenging");
                }

                return false;
            }

            if (!CanInterruptCurrentJobForScavenge(pawn, logDecision))
            {
                return false;
            }

            Job job = TryCreatePrisonerScavengeJob(pawn, logDecision);
            if (job == null)
            {
                return false;
            }

            bool accepted = pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
            if (logDecision)
            {
                LogPrisonerScavenge(pawn, accepted ? "start: ordered scavenging job accepted" : "fail: ordered scavenging job rejected", force: true);
            }

            return accepted;
        }

        private static bool CanInterruptCurrentJobForScavenge(Pawn pawn, bool logDecision)
        {
            bool burstActive = IsPrisonerScavengeBurstActive(pawn);

            Job currentJob = pawn?.CurJob;
            if (currentJob == null || currentJob.def == null)
            {
                return true;
            }

            if (IsScavengeInterruptBlockedJob(currentJob.def))
            {
                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: blocked by compatibility job=" + currentJob.def.defName);
                }

                return false;
            }

            if (currentJob.playerForced)
            {
                if (logDecision)
                {
                    LogPrisonerScavenge(pawn, "skip: current job is player-forced");
                }

                return false;
            }

            JobDef currentDef = currentJob.def;
            if (currentDef == JobDefOf.Wait || currentDef == JobDefOf.Wait_Wander || currentDef == JobDefOf.LayDown || currentDef == JobDefOf.GotoWander)
            {
                return true;
            }

            if (!burstActive && pawn.needs?.food != null && pawn.needs.food.CurCategory == HungerCategory.Starving)
            {
                return true;
            }

            if (logDecision)
            {
                LogPrisonerScavenge(pawn, burstActive
                    ? "skip: burst keeps current job=" + currentDef.defName
                    : "skip: keep current job=" + currentDef.defName);
            }

            return false;
        }

        private static bool IsScavengeInterruptBlockedJob(JobDef jobDef)
        {
            if (jobDef == null)
            {
                return false;
            }

            string defName = jobDef.defName ?? string.Empty;
            for (int i = 0; i < ScavengeInterruptBlockedJobKeywords.Length; i++)
            {
                if (defName.IndexOf(ScavengeInterruptBlockedJobKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            string driverClassName = jobDef.driverClass?.FullName;
            if (!driverClassName.NullOrEmpty())
            {
                for (int i = 0; i < ScavengeInterruptBlockedJobKeywords.Length; i++)
                {
                    if (driverClassName.IndexOf(ScavengeInterruptBlockedJobKeywords[i], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static void LogPrisonerScavenge(Pawn pawn, string message, bool force = false)
        {
            if (!IsPrisonerScavengeDebugLogEnabled || pawn == null)
            {
                return;
            }

            if (!force && PrisonerScavengeLastLogMessageByPawnId.TryGetValue(pawn.thingIDNumber, out string lastMessage) && lastMessage == message)
            {
                return;
            }

            int nowTick = Find.TickManager?.TicksGame ?? 0;
            if (!force && PrisonerScavengeLastLogTickByPawnId.TryGetValue(pawn.thingIDNumber, out int lastTick) && nowTick - lastTick < PrisonerScavengeLogIntervalTicks)
            {
                return;
            }

            PrisonerScavengeLastLogTickByPawnId[pawn.thingIDNumber] = nowTick;
            PrisonerScavengeLastLogMessageByPawnId[pawn.thingIDNumber] = message;
            Log.Message("[MouseDisaster][PrisonerScavenge] t=" + nowTick + " pawn=" + pawn.LabelShortCap + "#" + pawn.thingIDNumber + " " + message);
        }
    }
}
