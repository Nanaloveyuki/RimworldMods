namespace HungerAndHavoc.Incidents
{
    internal readonly struct RHAH_CampPlan
    {
        public int Adults { get; }
        public int Children { get; }
        public int Goodwill { get; }
        public int Days { get; }

        public RHAH_CampPlan(int adults, int children, int goodwill, int days)
        {
            Adults = adults;
            Children = children;
            Goodwill = goodwill;
            Days = days;
        }
    }

    internal static class RHAH_RefugeeCampRules
    {
        internal const int MinAdults = 2;
        internal const int MaxAdults = 4;
        internal const int MinChildren = 8;
        internal const int MaxChildren = 16;
        internal const int Goodwill = 12;
        internal const int Days = 15;
        internal const int Huts = 4;

        internal static RHAH_CampPlan Plan(int adultRoll, int childRoll)
        {
            Core.RHAH_Settings settings = Core.RHAH_Mod.Settings;
            int minAdults = settings == null ? MinAdults : settings.campMinAdults;
            int maxAdults = settings == null ? MaxAdults : settings.campMaxAdults;
            int minChildren = settings == null ? MinChildren : settings.campMinChildren;
            int maxChildren = settings == null ? MaxChildren : settings.campMaxChildren;
            int goodwill = settings == null ? Goodwill : settings.campGoodwill;
            int days = settings == null ? Days : settings.campDays;
            return new RHAH_CampPlan(Core.GameComponent_RHAH_FamineYear.ScalePopulation(Clamp(adultRoll, minAdults, maxAdults)),
                Core.GameComponent_RHAH_FamineYear.ScalePopulation(Clamp(childRoll, minChildren, maxChildren)), goodwill, days);
        }

        internal static bool CanOffer(bool violentQuests, bool campEnabled, bool hasSponsor, bool tileValid)
        {
            return violentQuests && campEnabled && hasSponsor && tileValid;
        }

        internal static bool AllowedWeapon(string defName, bool coreMod, bool weapon, bool melee, int tech, bool shortBow, bool wooden)
        {
            return weapon && RHAH_PredationRules.VanillaWeapon(defName, coreMod, melee, tech, shortBow, wooden);
        }

        static int Clamp(int value, int min, int max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
