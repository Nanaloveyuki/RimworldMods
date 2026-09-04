using Verse;

namespace SaveYourStorageAndTickEveryMinute
{
    public sealed class SaveYourStorageSettings : ModSettings
    {
        public bool listerMembershipIndex = true;
        public bool outerrealmLeaseThrottle = true;
        public int outerrealmMaximumRehomesPerTick = 8;
        public float outerrealmMaximumMilliseconds = 0.5f;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref listerMembershipIndex, "listerMembershipIndex", true);
            Scribe_Values.Look(ref outerrealmLeaseThrottle, "outerrealmLeaseThrottle", true);
            Scribe_Values.Look(
                ref outerrealmMaximumRehomesPerTick,
                "outerrealmMaximumRehomesPerTick",
                8);
            Scribe_Values.Look(
                ref outerrealmMaximumMilliseconds,
                "outerrealmMaximumMilliseconds",
                0.5f);
            base.ExposeData();
        }

        public void Normalize()
        {
            outerrealmMaximumRehomesPerTick = Clamp(
                outerrealmMaximumRehomesPerTick,
                1,
                64);
            outerrealmMaximumMilliseconds = Clamp(
                outerrealmMaximumMilliseconds,
                0.05f,
                5f);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
