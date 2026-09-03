using Verse;

namespace OhYouAreSoFast
{
    public sealed class OhYouAreSoFastSettings : ModSettings
    {
        public bool adaptivePathHeuristic = true;
        public bool worldPathCorrectness = true;
        public bool sparseDynamicPathData = true;
        public bool workScanThrottle = true;
        public int workScanIntervalSeconds = 5;
        public bool workPriorityCache = true;
        public int workPriorityCacheIntervalSeconds = 10;
        public int idleThinkMinMapCells = 20000;
        public bool saveLoadTrace = true;
        public bool saveLoadFullTrace;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref adaptivePathHeuristic, "adaptivePathHeuristic", true);
            Scribe_Values.Look(ref worldPathCorrectness, "worldPathCorrectness", true);
            Scribe_Values.Look(ref sparseDynamicPathData, "sparseDynamicPathData", true);
            Scribe_Values.Look(ref workScanThrottle, "workScanThrottle", true);
            Scribe_Values.Look(ref workScanIntervalSeconds, "workScanIntervalSeconds", 5);
            Scribe_Values.Look(ref workPriorityCache, "workPriorityCache", true);
            Scribe_Values.Look(ref workPriorityCacheIntervalSeconds, "workPriorityCacheIntervalSeconds", 10);
            Scribe_Values.Look(ref idleThinkMinMapCells, "idleThinkMinMapCells", 20000);
            Scribe_Values.Look(ref saveLoadTrace, "saveLoadTrace", true);
            Scribe_Values.Look(ref saveLoadFullTrace, "saveLoadFullTrace", false);
            base.ExposeData();
        }

        public void Normalize()
        {
            workScanIntervalSeconds = Clamp(workScanIntervalSeconds, 1, 60);
            workPriorityCacheIntervalSeconds = Clamp(workPriorityCacheIntervalSeconds, 1, 60);
            idleThinkMinMapCells = Clamp(idleThinkMinMapCells, 0, 2000000);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
