using Verse;

namespace MouseDisaster
{
    public class MapComponent_MouseDisasterCompat : MapComponent
    {
        private const int HiddenFactionMaintenanceIntervalTicks = 1800;
        private const int ChildExchangeUpdateIntervalTicks = 60;
        private const int AbandonedDeliveryUpdateIntervalTicks = 60;
        private const int ChaosPregnancyUpdateIntervalTicks = 60;
        private const int DoorStuckRecoveryIntervalTicks = 60;
        private const int BabyExpansionMoodUpdateIntervalTicks = 120;
        private const int SleepPoopUpdateIntervalTicks = 250;
        private const int AidRequestVisitorUpdateIntervalTicks = 60;
        private const int RatkinActivityUpdateIntervalTicks = 60;

        public MapComponent_MouseDisasterCompat(Map map)
            : base(map)
        {
            MouseDisasterUtility.EnsureReliefAreaExists(map);
        }

        public override void MapComponentTick()
        {
            if (ShouldRun(HiddenFactionMaintenanceIntervalTicks))
            {
                MouseDisasterUtility.TryRefreshHiddenFactionRelations();
                MouseDisasterUtility.TryRepairMissingFactionRelations();
            }

            if (MouseDisasterUtility.HasActiveChildExchangeState() && ShouldRun(ChildExchangeUpdateIntervalTicks))
            {
                MouseDisasterUtility.ProcessChildExchangeTimeouts(map);
            }

            if (MouseDisasterUtility.HasActiveAbandonedDeliveryState() && ShouldRun(AbandonedDeliveryUpdateIntervalTicks))
            {
                MouseDisasterUtility.ProcessAbandonedDeliveries(map);
            }

            if (MouseDisasterMod.Settings?.forceDismissMobileNonInfantEventPawns == true &&
                ShouldRun(AbandonedDeliveryUpdateIntervalTicks))
            {
                MouseDisasterUtility.ForceDismissMobileNonInfantEventPawns(map, onlyWaiting: true);
            }

            if (MouseDisasterPhase2Utility.HasActiveAidRequestVisitors() && ShouldRun(AidRequestVisitorUpdateIntervalTicks))
            {
                MouseDisasterPhase2Utility.ProcessAidRequestVisitors(map);
            }

            if (ShouldRun(ChaosPregnancyUpdateIntervalTicks))
            {
                MouseDisasterUtility.ProcessChaosPregnancies(map);
            }

            if (ShouldRun(DoorStuckRecoveryIntervalTicks))
            {
                MouseDisasterUtility.ProcessOpenDoorStuckJobs(map);
            }

            if (ShouldRun(BabyExpansionMoodUpdateIntervalTicks))
            {
                MouseDisasterUtility.ProcessBabyExpansionMoodReplacement(map);
            }

            if (ShouldRun(SleepPoopUpdateIntervalTicks))
            {
                MouseDisasterUtility.TrySpawnSleepPoopFilth(map);
            }
        }

        public override void MapRemoved()
        {
            MouseDisasterUtility.CleanupExpiredPendingState();
            MouseDisasterPhase2Utility.CleanupExpiredPendingState();
            base.MapRemoved();
        }

        private bool ShouldRun(int intervalTicks)
        {
            if (intervalTicks <= 1 || Find.TickManager == null)
            {
                return true;
            }

            return (Find.TickManager.TicksGame + map.uniqueID) % intervalTicks == 0;
        }
    }
}
