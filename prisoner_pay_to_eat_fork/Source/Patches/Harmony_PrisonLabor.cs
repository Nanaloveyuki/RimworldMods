using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace PrisonersPayToEat2
{
    /// <summary>
    /// Bridges Avius's "Prison Labor" MOD (packageId avius.prisonlabor).
    /// Prison Labor decides which work types a prisoner may do; we just detect "is currently doing
    /// a work job" via <see cref="CurrentWorkTypeDef"/> and pay the wage configured for that type.
    /// (Prison Labor's Need_Motivation.IsPrisonerWorking is never actually set true, so we can't use it.)
    /// </summary>
    public static class PrisonLaborBridge
    {
        public const string PackageId = "avius.prisonlabor";

        private static bool? _present;
        public static bool Present
        {
            get
            {
                if (_present.HasValue) return _present.Value;
                _present = false;
                foreach (var mc in LoadedModManager.RunningModsListForReading)
                {
                    if (!string.Equals(mc.PackageId, PackageId, StringComparison.OrdinalIgnoreCase)) continue;
                    // Version declarations do not determine whether an enabled mod is present.
                    _present = true;
                    break;
                }
                return _present.Value;
            }
        }

        /// <summary>Recognized labor, excluding death and mental breaks.</summary>
        public static bool IsWorking(Pawn pawn)
        {
            if (pawn == null || pawn.Dead) return false;
            if (pawn.InMentalState) return false;
            return WorkBilling.IsKnown(WorkAttribution.Resolve(pawn, pawn.jobs?.curJob));
        }

        /// <summary>WorkTypeDef of the pawn's current work job, or null when not working.</summary>
        public static WorkTypeDef CurrentWorkTypeDef(Pawn pawn)
        {
            return WorkAttribution.Resolve(pawn, pawn?.jobs?.curJob).WorkType;
        }
    }

    internal readonly struct AttributedWork
    {
        internal readonly WorkTypeDef WorkType;
        internal readonly WorkGiverDef Giver;
        internal readonly bool WheelSession;

        internal AttributedWork(WorkTypeDef workType, bool wheelSession, WorkGiverDef giver = null)
        {
            WorkType = workType;
            WheelSession = wheelSession;
            Giver = giver;
        }

        // Verified against the installed NewRatkin 1.7 assembly: walk, then a 4000-tick work toil.
        // Natural completion advances past both; a timer or cleanup callback does not prove this.
        internal bool CanComplete(JobDriver driver) => !WheelSession
            || (driver != null && driver.CurToilIndex == 2 && driver.ticksLeftThisToil <= 0);
    }

    internal static class WorkAttribution
    {
        private static JobDef wheelJob;
        private static WorkTypeDef wheelWork;
        private static ThingDef wheelTarget;
        private static Type wheelDriver;

        internal static void Initialize()
        {
            wheelJob = null;
            wheelWork = null;
            wheelTarget = null;
            wheelDriver = null;
            bool present = false;
            foreach (var mod in LoadedModManager.RunningModsListForReading)
                if (string.Equals(mod.PackageId, "Solaris.RatkinRaceMod", StringComparison.OrdinalIgnoreCase))
                    present = true;
            if (!present) return;

            var job = DefDatabase<JobDef>.GetNamedSilentFail("RK_Job_HamsterWheel");
            var giver = DefDatabase<WorkGiverDef>.GetNamedSilentFail("RK_Work_HamsterWheel");
            var work = DefDatabase<WorkTypeDef>.GetNamedSilentFail("RK_WorkType_GeneratePower");
            var target = DefDatabase<ThingDef>.GetNamedSilentFail("RK_HamsterWheelGenerator");
            if (job?.driverClass?.FullName != "NewRatkin.JobDriver_HamsterWheel"
                || giver?.giverClass?.FullName != "NewRatkin.WorkGiver_HamsterWheel"
                || work == null || giver.workType != work || target == null)
            {
                Log.Warning("[PPTE2] NewRatkinPlus wheel definitions differ from the supported 1.7 implementation; direct-job wage attribution is unavailable.");
                return;
            }
            wheelJob = job;
            wheelWork = work;
            wheelTarget = target;
            wheelDriver = job.driverClass;
        }

        internal static AttributedWork Resolve(Pawn pawn, Job job)
        {
            if (job == null) return default;
            var giver = job.workGiverDef;
            var nativeWork = giver?.workType;
            var driver = pawn?.jobs?.curDriver;
            bool wheel = wheelJob != null && job.def == wheelJob
                && job.targetA.Thing?.def == wheelTarget && driver?.job == job
                && driver.GetType() == wheelDriver;
            if (nativeWork != null)
                return new AttributedWork(nativeWork, wheel && nativeWork == wheelWork, giver);
            if (wheel) return new AttributedWork(wheelWork, true, giver);
            return new AttributedWork(null, false, giver);
        }
    }

    /// <summary>
    /// Pays out hourly work wages. Runs from <see cref="PrisonersPayToEat2Manager"/>.
    /// Fractions accumulate per tick and are credited immediately (>=0.01) so short shifts still pay;
    /// a leftover fraction is flushed the moment a work session ends.
    /// </summary>
    public static class PrisonLaborWageTicker
    {
        private const int TicksPerHour = 2500;
        private const int CheckStride = 10; // check every 10 ticks to avoid per-tick GC churn

        // remembers whether a pawn was working at the last check, to settle the accumulator
        // the moment a work session ends
        private static readonly Dictionary<int, bool> wasWorking = new Dictionary<int, bool>();
        private static readonly HashSet<int> livePrisoners = new HashSet<int>();
        private static readonly List<int> staleWorkers = new List<int>();

        /// <summary>读档/换存档时清空进程级缓存（thingIDNumber 跨存档会复用，不能留着）。</summary>
        public static void ResetTransientState()
        {
            wasWorking.Clear();
            livePrisoners.Clear();
            staleWorkers.Clear();
        }

        public static void Tick()
        {
            var game = Current.Game;
            if (game == null) return;
            int ticks = game.tickManager.TicksGame;
            if (ticks % CheckStride != 0) return;
            // Keep the existing labour-provider gate; only move it behind the cadence check.
            if (!PrisonerWorkSystem.WorkIncomeEnabled) return;
            var settings = PrisonersPayToEat2Mod.Settings;
            var mgr = PrisonersPayToEat2Manager.Current;
            if (mgr == null) return;
            bool hourlySettle = ticks % TicksPerHour == 0;
            if (hourlySettle) livePrisoners.Clear();

            foreach (var map in game.Maps)
            {
                if (map == null) continue;
                var list = mgr.PrisonersOn(map);
                for (int i = 0; i < list.Count; i++)
                {
                    var pawn = list[i];
                    if (pawn == null) continue;
                    int id = pawn.thingIDNumber;
                    if (hourlySettle) livePrisoners.Add(id);
                    if (pawn.Dead) continue;

                    var work = pawn.InMentalState ? default : WorkAttribution.Resolve(pawn, pawn.jobs?.curJob);
                    if (WorkBilling.IsKnown(work))
                    {
                        if (PieceRateWorker.IsPieceRateActive(pawn, work))
                        {
                            // piece-rate mode: no hourly accrual (settlement happens on the output
                            // hooks); for research we only track contribution and pay on completion
                            if (WorkBilling.UsesUnit(work, WorkUnit.Research))
                                PieceRateWorker.RecordResearchContribution(pawn, CheckStride);
                        }
                        else
                        {
                            string workTypeDefName = WorkBilling.HasIndividualRates(work)
                                ? work.Giver.defName : work.WorkType?.defName;
                            float wagePerHour = WorkBilling.HourlyWage(work);
                            float perCheck = wagePerHour * settings.wageMultiplier * mgr.EffectiveWageMultiplier(pawn)
                                             * CheckStride / TicksPerHour;

                            float accum = mgr.GetWorkWageAccum(id) + perCheck;
                            if (accum >= 0.01f) // fractional tickets: credit as soon as we have any
                            {
                                mgr.AddTickets(pawn, accum);
                                Need_Motivation.NotifyWage(pawn, accum);
                                if (settings.logVerbose)
                                    Log.Message($"[PPTE2] {pawn.LabelShortCap} earned {accum:0.##} tickets for working {workTypeDefName ?? "??"}. Balance={mgr.Balance(pawn):0.##}");
                                accum = 0f;
                            }
                            mgr.SetWorkWageAccum(id, accum);
                        }
                        wasWorking[id] = true;
                    }
                    else if (wasWorking.TryGetValue(id, out bool worked) && worked)
                    {
                        // work session ended: flush the leftover fraction so short shifts pay out
                        float accum = mgr.GetWorkWageAccum(id);
                        if (accum >= 0.01f)
                        {
                            mgr.AddTickets(pawn, accum);
                            Need_Motivation.NotifyWage(pawn, accum);
                            if (settings.logVerbose)
                                Log.Message($"[PPTE2] {pawn.LabelShortCap} settled {accum:0.##} leftover work. Balance={mgr.Balance(pawn):0.##}");
                        }
                        mgr.SetWorkWageAccum(id, 0f);
                        wasWorking[id] = false;
                    }
                }
            }

            if (hourlySettle)
            {
                // prune state for prisoners that left the map or died
                staleWorkers.Clear();
                foreach (var kv in wasWorking)
                    if (!livePrisoners.Contains(kv.Key)) staleWorkers.Add(kv.Key);
                foreach (var k in staleWorkers) { wasWorking.Remove(k); mgr.SetWorkWageAccum(k, 0f); }
                staleWorkers.Clear();
            }
        }
    }
}